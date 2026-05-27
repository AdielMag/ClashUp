namespace ClashUp.Server.GameServer.Registration;

/// <summary>
/// Holds the runtime-resolved instance id (config-provided or freshly
/// generated) and the inter-tier token Services handed back on
/// RegisterAsync. Populated by GameServerRegistrar before the
/// heartbeat service starts ticking.
/// </summary>
public sealed class GameServerIdentity
{
    public string InstanceId { get; set; } = string.Empty;
    public string BearerJwt { get; set; } = string.Empty;
}
