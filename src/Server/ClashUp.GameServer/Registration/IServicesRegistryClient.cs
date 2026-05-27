using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Registration;

/// <summary>
/// Thin abstraction over the MagicOnion client to IGameServerRegistry on
/// the Services tier. Lets us swap the transport in tests.
/// </summary>
public interface IServicesRegistryClient
{
    Task<GsToken> RegisterAsync(GsRegistration registration, CancellationToken cancellationToken);
    Task HeartbeatAsync(GsHeartbeat heartbeat, CancellationToken cancellationToken);
}
