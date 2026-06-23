using System.Linq;
using ClashUp.Server.Common.Auth;
using ClashUp.Server.GameServer.Abilities;
using ClashUp.Server.GameServer.Ccu;
using ClashUp.Server.GameServer.Match;
using ClashUp.Server.GameServer.Registration;
using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Characters;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.GameServer.Hubs;

/// <summary>
/// Per-match StreamingHub. Validates the MatchToken JWT, registers the
/// connection with the match's Group, and enqueues inputs into the
/// per-match InputBuffer. No sim work happens here — see
/// docs/rules/magiconion-hub-discipline.md.
/// </summary>
public sealed class MatchHub : StreamingHubBase<IMatchHub, IMatchHubReceiver>, IMatchHub
{
    private readonly IMatchRegistry _matches;
    private readonly IMatchTokenValidator _tokens;
    private readonly GameServerIdentity _identity;
    private readonly IServicesRegistryClient _servicesClient;
    private readonly ServerAbilityStore _abilityStore;
    private readonly ICcuTracker _ccuTracker;

    private MatchContext? _context;
    private MatchTokenClaims _claims;
    private bool _left;

    public MatchHub(IMatchRegistry matches, IMatchTokenValidator tokens, GameServerIdentity identity, IServicesRegistryClient servicesClient, ServerAbilityStore abilityStore, ICcuTracker ccuTracker)
    {
        _matches = matches;
        _tokens = tokens;
        _identity = identity;
        _servicesClient = servicesClient;
        _abilityStore = abilityStore;
        _ccuTracker = ccuTracker;
    }

    public async Task<JoinResult> JoinAsync(MatchJoinRequest request)
    {
        _claims = _tokens.Validate(request.MatchToken);

        if (!string.Equals(_claims.MatchId, request.MatchId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("MatchToken.matchId does not match the requested match.");
        }

        if (!string.IsNullOrEmpty(_identity.InstanceId)
            && !string.Equals(_claims.GsInstanceId, _identity.InstanceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"MatchToken issued for GS {_claims.GsInstanceId}, not this instance ({_identity.InstanceId}).");
        }

        if (!_matches.TryGet(request.MatchId, out var context))
        {
            // Match was already cleaned up. Re-fire the ended notification so the
            // Services DB catches up (the original fire-and-forget may not have landed yet).
            _ = _servicesClient.ReportMatchEndedAsync(
                new GsMatchEnded { InstanceId = _identity.InstanceId, MatchId = request.MatchId },
                CancellationToken.None);
            throw new InvalidOperationException($"Match {request.MatchId} has already ended.");
        }
        _context = context;

        // A player who forfeited this match is permanently barred — never route them back in.
        if (context.IsForfeited(_claims.PlayerId))
        {
            throw new InvalidOperationException($"Player {_claims.PlayerId} forfeited match {request.MatchId}.");
        }

        var group = await Group.AddAsync(context.MatchId.Value);
        context.Group ??= group;

        PlayerSummary summary;
        if (context.IsPlayerInMatch(_claims.PlayerId))
        {
            context.MarkConnected(_claims.PlayerId);
            summary = context.GetPlayers().First(p => p.Id.Value == _claims.PlayerId);
        }
        else
        {
            var assignment = context.Provision.PlayerAssignments
                .FirstOrDefault(a => a.PlayerId.Value == _claims.PlayerId);

            // Honor the player's chosen character; the catalog normalizes unknown/empty ids to the
            // default, so the server stays authoritative even against a stale or malicious client.
            var catalog = CharacterCatalog.FromConfig(context.Provision.Characters);
            var chosenCharacter = catalog.Get(request.CharacterId).Id;
            summary = new PlayerSummary
            {
                Id = new PlayerId(_claims.PlayerId),
                DisplayName = $"Player-{_claims.PlayerId[..6]}",
                TeamId = assignment?.TeamId ?? 0,
                ColorSlot = context.GetPlayers().Count,
                CharacterId = chosenCharacter,
            };
            context.AddPlayer(summary);
        }

        context.Group?.All.OnPlayerJoined(summary);

        // Count this player toward instance CCU (cancels any pending disconnect-grace removal).
        _ccuTracker.PlayerConnected(_claims.PlayerId);

        // If the match already ended (client reconnected during the 2-second grace window),
        // replay OnMatchEnded directly to this client so it doesn't get stuck at 00:00.
        if (context.IsEnded && context.EndResult is { } endResult)
            Client.OnMatchEnded(endResult);

        return new JoinResult
        {
            You = summary.Id,
            Players = context.GetPlayers(),
            TickRateHz = context.Provision.TickRateHz,
            CurrentTick = context.Simulation.CurrentTick,
            DurationSeconds = context.Provision.DurationSeconds,
            ElapsedSeconds = context.Clock.ElapsedSeconds,
            RandomSeed = context.Simulation.RandomSeed,
            MapId = context.Provision.MapId,
            Characters = context.Provision.Characters,
            Abilities = _abilityStore.BuildClientConfig(),
        };
    }

    public async Task LeaveAsync()
    {
        if (_context is null || _left)
        {
            return;
        }
        _left = true;

        // Forfeit: permanently remove the player from the match (vs. a disconnect, which is
        // reconnectable). Dropping them from the roster is the signal the tick loop reads to
        // re-evaluate the alive-team count and end the match if only one team is left.
        _context.Forfeit(_claims.PlayerId);
        _ccuTracker.PlayerDisconnected(_claims.PlayerId);

        // Remove from the Services match doc so reconnect lookups stop routing them back here.
        // Awaited (not fire-and-forget) so that by the time the client's LeaveAsync returns,
        // the DB no longer lists the player — otherwise the lobby's CheckActiveMatchAsync races
        // the Mongo write and reconnects them right back into the match.
        try
        {
            await _servicesClient.ReportPlayerLeftAsync(
                new GsPlayerLeft
                {
                    InstanceId = _identity.InstanceId,
                    MatchId = _context.MatchId,
                    PlayerId = _claims.PlayerId,
                },
                CancellationToken.None);
        }
        catch
        {
            // Best-effort; the GS-local _forfeited barrier still bars rejoin while the match runs.
        }

        _context.Group?.All.OnPlayerLeft(new PlayerId(_claims.PlayerId), LeaveReason.ClientLeave);
        if (_context.Group is not null)
        {
            await _context.Group.RemoveAsync(Context);
        }
    }

    public Task SubmitInputAsync(InputCommand command)
    {
        _context?.Inputs.Enqueue(new PlayerInput(new PlayerId(_claims.PlayerId), command));
        return Task.CompletedTask;
    }

    public Task<PongResult> PingAsync(long clientStampMs)
    {
        var result = new PongResult
        {
            ClientStampMs = clientStampMs,
            ServerStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        return Task.FromResult(result);
    }

    protected override async ValueTask OnDisconnected()
    {
        // A voluntary LeaveAsync (forfeit) already handled teardown; the socket close that
        // follows must not re-mark the (now removed) player as a reconnectable disconnect.
        if (_context is not null && !_left)
        {
            _context.MarkDisconnected(_claims.PlayerId);
            // Start the CCU grace timer; reconnecting before it elapses keeps the player counted.
            _ccuTracker.PlayerDisconnected(_claims.PlayerId);
            _context.Group?.All.OnPlayerLeft(new PlayerId(_claims.PlayerId), LeaveReason.Disconnect);
            if (_context.Group is not null)
                await _context.Group.RemoveAsync(Context);
        }
    }
}
