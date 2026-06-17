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

    /// <summary>
    /// Address clients reach this GS on, resolved at registration time (config,
    /// or the GCE external IP when running on Compute Engine). The heartbeat
    /// retry path reuses this so it stays consistent with the first registration.
    /// </summary>
    public string PublicEndpoint { get; set; } = string.Empty;

    /// <summary>Address the Services tier uses to call this GS (defaults to PublicEndpoint).</summary>
    public string InternalEndpoint { get; set; } = string.Empty;
}
