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
    private readonly CharacterConfigProvider _characterConfigProvider;
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
        CharacterConfigProvider characterConfigProvider,
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
        _characterConfigProvider = characterConfigProvider;
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
        // Each queued mode forms its own batches against its own config.
        foreach (var modeId in _queue.GetQueuedModeIds())
        {
            var config = await _configProvider.GetAsync(modeId, ct);
            var matchSize = config.NumberOfTeams * config.TeamSize;
            if (matchSize <= 0) continue;

            var batch = _queue.TryDrain(matchSize, modeId);
            if (batch is not null)
            {
                await ProvisionMatchAsync(modeId, config, batch, botCount: 0, ct);
                continue;
            }

            // Bot-fill fallback: once the oldest waiting player has waited long enough, form a
            // partial human batch and fill the remaining slots with AI bots.
            if (config.FillWithBots)
            {
                var oldest = _queue.OldestEnqueuedAt(modeId);
                if (oldest is { } enqueuedAt
                    && _queue.CountQueued(modeId) >= config.MinRealPlayers
                    && (DateTime.UtcNow - enqueuedAt).TotalSeconds >= config.BotFillWaitSeconds)
                {
                    var partial = _queue.DrainUpTo(matchSize, modeId);
                    if (partial.Count > 0)
                        await ProvisionMatchAsync(modeId, config, partial, matchSize - partial.Count, ct);
                }
            }
        }
    }

    private async Task ProvisionMatchAsync(string modeId, MatchConfig config, List<TicketEntry> batch, int botCount, CancellationToken ct)
    {
        var characters = await _characterConfigProvider.GetAsync(ct);

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

        // Real players take slots 0..N-1; bots fill N..matchSize-1. Teams continue the modulo
        // assignment across all slots so bots distribute the same way real players would.
        var assignments = new List<PlayerAssignment>(batch.Count + botCount);
        for (int i = 0; i < batch.Count; i++)
        {
            assignments.Add(new PlayerAssignment
            {
                PlayerId = new PlayerId(batch[i].PlayerId),
                TeamId = i % config.NumberOfTeams,
                IsBot = false,
            });
        }
        for (int j = 0; j < botCount; j++)
        {
            int slot = batch.Count + j;
            assignments.Add(new PlayerAssignment
            {
                PlayerId = new PlayerId("bot:" + Guid.NewGuid().ToString("N")),
                TeamId = slot % config.NumberOfTeams,
                IsBot = true,
            });
        }

        var provision = new MatchProvision
        {
            MatchId = new MatchId(matchId),
            // Only real players receive match tokens / connect — bots never join the hub.
            Players = batch.Select(b => new PlayerId(b.PlayerId)).ToList(),
            ModeId = modeId,
            TickRateHz = _options.DefaultTickRateHz,
            DurationSeconds = config.DurationSeconds,
            MapId = config.MapId,
            PlayerAssignments = assignments,
            Characters = characters,
            ObjectiveType = config.ObjectiveType,
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
