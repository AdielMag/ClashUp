using ClashUp.Shared.MessagePackObjects;
using MagicOnion;

namespace ClashUp.Shared.Services;

/// <summary>
/// Internal gRPC service hosted by Services. Game servers call
/// RegisterAsync on startup, HeartbeatAsync every ~2s, and
/// ReportMatchStartedAsync / ReportMatchEndedAsync as matches transition.
/// Must be gated by inter-tier auth in production — see
/// docs/rules/jwt-auth.md.
/// </summary>
public interface IGameServerRegistry : IService<IGameServerRegistry>
{
    UnaryResult<GsToken> RegisterAsync(GsRegistration registration);

    UnaryResult HeartbeatAsync(GsHeartbeat heartbeat);

    UnaryResult ReportMatchStartedAsync(GsMatchStarted notice);

    UnaryResult ReportMatchEndedAsync(GsMatchEnded notice);
}
