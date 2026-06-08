using ClashUp.Server.Common.Auth;
using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.Services.Matchmaking;

/// <summary>
/// Background service that drains the matchmaking queue, places groups
/// onto game servers, and resolves tickets with a MatchHandoff. Phase-1
/// placement: simple "least-loaded GS" pick. See docs/rules/server-authority.md
/// — handoff JWT minting happens here.
/// </summary>
public sealed class Matchmaker : BackgroundService
{
    private readonly MatchmakingQueue _queue;
    private readonly IGameServerInstanceRepository _gsRepo;
    private readonly IMatchRepository _matchRepo;
    private readonly IGameServerProvisioner _provisioner;
    private readonly GameServerAdminClientFactory _adminClients;
    private readonly IJwtTokenIssuer _tokens;
    private readonly MatchConfigProvider _configProvider;
    private readonly MatchmakingOptions _options;
    private readonly ILogger<Matchmaker> _logger;

    public Matchmaker(
        MatchmakingQueue queue,
        IGameServerInstanceRepository gsRepo,
        IMatchRepository matchRepo,
        IGameServerProvisioner provisioner,
        GameServerAdminClientFactory adminClients,
        IJwtTokenIssuer tokens,
        MatchConfigProvider configProvider,
        IOptions<MatchmakingOptions> options,
        ILogger<Matchmaker> logger)
    {
        _queue = queue;
        _gsRepo = gsRepo;
        _matchRepo = matchRepo;
        _provisioner = provisioner;
        _adminClients = adminClients;
        _tokens = tokens;
        _configProvider = configProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(50, _options.DrainIntervalMs));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DrainOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Matchmaker drain failed");
            }
        }
    }

    private async Task DrainOnceAsync(CancellationToken ct)
    {
        var modeId = "default";
        var config = await _configProvider.GetAsync(modeId, ct);
        var matchSize = config.NumberOfTeams * config.TeamSize;

        var batch = _queue.TryDrain(matchSize);
        if (batch is null)
        {
            return;
        }

        var gs = await PickGameServerAsync(ct);
        if (gs is null)
        {
            var resp = await _provisioner.RequestNewInstanceAsync(ct);
            FailBatch(batch, resp.Reason);
            return;
        }

        var matchId = Guid.NewGuid().ToString("N");
        var matchDoc = new MatchDoc
        {
            MatchId = matchId,
            GsInstanceId = gs.InstanceId,
            GsEndpoint = gs.PublicEndpoint,
            ModeId = modeId,
            State = "Provisioning",
            DurationSeconds = config.DurationSeconds,
            CreatedAt = DateTime.UtcNow,
            Players = batch.Select((b, i) => new MatchPlayerDoc { PlayerId = b.PlayerId, TeamId = i % config.NumberOfTeams }).ToList(),
        };
        await _matchRepo.InsertAsync(matchDoc, ct);

        var provision = new MatchProvision
        {
            MatchId = new MatchId(matchId),
            Players = batch.Select(b => new PlayerId(b.PlayerId)).ToList(),
            ModeId = modeId,
            TickRateHz = _options.DefaultTickRateHz,
            DurationSeconds = config.DurationSeconds,
            MapId = config.MapId,
        };

        try
        {
            var serviceEndpoint = string.IsNullOrWhiteSpace(gs.InternalEndpoint)
                ? gs.PublicEndpoint
                : gs.InternalEndpoint;
            var adminClient = _adminClients.GetOrCreate(serviceEndpoint);
            await adminClient.PrepareMatchAsync(provision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PrepareMatchAsync failed on GS {InstanceId}", gs.InstanceId);
            FailBatch(batch, "gs_provision_failed");
            await _matchRepo.SetStateAsync(matchId, "Ended", ct);
            return;
        }

        await _matchRepo.SetStateAsync(matchId, "Active", ct);

        foreach (var ticket in batch)
        {
            var token = _tokens.IssueMatchToken(ticket.PlayerId, matchId, gs.InstanceId);
            ticket.Handoff = new MatchHandoff
            {
                MatchId = new MatchId(matchId),
                GsEndpoint = gs.PublicEndpoint,
                MatchToken = token.Jwt,
                MatchTokenExpiresAtMs = new DateTimeOffset(token.ExpiresAt).ToUnixTimeMilliseconds(),
            };
            ticket.Status = TicketStatus.Matched;
        }
    }

    private async Task<GameServerInstanceDoc?> PickGameServerAsync(CancellationToken ct)
    {
        var candidates = await _gsRepo.ListHealthyAsync(ct);
        return candidates
            .Where(c => c.CapacityUsed < c.CapacityMax)
            .OrderByDescending(c => c.CapacityMax - c.CapacityUsed)
            .ThenBy(c => c.CapacityUsed)
            .FirstOrDefault();
    }

    private static void FailBatch(List<TicketEntry> batch, string reason)
    {
        foreach (var entry in batch)
        {
            entry.Status = TicketStatus.Failed;
            entry.FailureReason = reason;
        }
    }
}
