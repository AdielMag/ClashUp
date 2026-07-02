namespace ClashUp.Tools.FleetController;

/// <summary>Bound from the <c>Fleet</c> config section (Cloud Run env vars in prod).</summary>
public sealed class FleetControllerOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string Region { get; set; } = "us-central1";

    /// <summary>Cloud Scheduler job that pings <c>/tick</c>. Paused on sleep, resumed on wake.</summary>
    public string SchedulerJob { get; set; } = "clashup-idle-check";

    public string ServicesMig { get; set; } = "clashup-services-mig";
    public string ServicesAutoscaler { get; set; } = "clashup-services-autoscaler";
    public string GameServerMig { get; set; } = "clashup-gameserver-mig";
    public string GameServerAutoscaler { get; set; } = "clashup-gameserver-autoscaler";

    /// <summary>
    /// How far back to look for CCU before declaring the fleet idle. Kept larger than
    /// the scheduler interval (30 min) so a single between-matches dip can't trigger sleep —
    /// the whole window must read zero.
    /// </summary>
    public int IdleLookbackMinutes { get; set; } = 35;

    // --- Ephemeral networking (torn down on sleep, recreated on wake) --------
    // These four resources are the entire idle-networking bill. On sleep the
    // controller deletes the forwarding rule + Cloud NAT and RELEASES both static
    // IPs (→ $0 while asleep). On wake it re-allocates fresh IPs, re-points the
    // forwarding rule at the still-live backend service, recreates the NAT, and
    // re-allowlists the new NAT IP in MongoDB Atlas. The client discovers the new
    // Services IP at boot via /resolve, so it is never baked into the build.

    /// <summary>Name of the regional external IP that fronts the Services L4 LB.</summary>
    public string ServicesIpName { get; set; } = "clashup-services-ip";

    /// <summary>Forwarding rule that binds the Services IP to the backend service.</summary>
    public string ServicesForwardingRule { get; set; } = "clashup-services-l4-fr";

    /// <summary>Region backend service (TF-owned, persists asleep) the rule targets.</summary>
    public string ServicesBackendService { get; set; } = "clashup-services-l4-backend";

    /// <summary>TCP port the Services gateway listens on (h2c gRPC).</summary>
    public int ServicesPort { get; set; } = 5001;

    /// <summary>Cloud Router (TF-owned, persists asleep) that hosts the NAT config.</summary>
    public string Router { get; set; } = "clashup-router";

    /// <summary>Cloud NAT config name added to / removed from the router.</summary>
    public string NatName { get; set; } = "clashup-nat";

    /// <summary>Name of the regional external IP used as the manual NAT egress IP.</summary>
    public string NatIpName { get; set; } = "clashup-nat-ip";

    // --- MongoDB Atlas allowlist automation ---------------------------------
    // The NAT egress IP must be allowlisted in Atlas Network Access or Services
    // instances can't reach Mongo. Since the NAT IP is re-allocated each wake, the
    // controller adds the fresh IP (and prunes stale ones it previously added).

    /// <summary>Atlas Admin API public key (HTTP Digest username).</summary>
    public string AtlasPublicKey { get; set; } = string.Empty;

    /// <summary>Atlas Admin API private key (HTTP Digest password). From Secret Manager.</summary>
    public string AtlasPrivateKey { get; set; } = string.Empty;

    /// <summary>Atlas project (group) id that owns the cluster + access list.</summary>
    public string AtlasProjectId { get; set; } = string.Empty;

    /// <summary>Comment stamped on access-list entries we own, so we only prune our own.</summary>
    public string AtlasEntryComment { get; set; } = "clashup-nat (fleet-controller managed)";

    // --- Public /resolve endpoint auth --------------------------------------

    /// <summary>Shared key the client sends on /resolve (bounded-cost wake gate).</summary>
    public string ResolveKey { get; set; } = string.Empty;

    /// <summary>Admin key the scheduler + dashboard send on /tick, /wake, /state.</summary>
    public string AdminKey { get; set; } = string.Empty;
}
