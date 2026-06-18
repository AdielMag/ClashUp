namespace ClashUp.Server.Gateway.Supervisor;

/// <summary>A version-specific backend container the supervisor is managing.</summary>
public sealed class BackendProcess
{
    public required string Version { get; init; }
    public required string ContainerId { get; init; }
    public required int HostPort { get; init; }

    /// <summary>Last time a request was routed to this backend (drives idle eviction).</summary>
    public DateTimeOffset LastUsedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Latest result of the maintenance-loop health probe.</summary>
    public bool Healthy { get; set; } = true;

    /// <summary>
    /// Pinned backends are exempt from idle eviction. The GameServer tier prewarms
    /// one backend whose sole job is to keep the instance registered + heartbeating
    /// with Services; if it idle-evicted, the instance would fall out of the
    /// registry (the backend marks itself Draining on SIGTERM) and matchmaking would
    /// have nowhere to place matches. Pinning keeps that backend alive for the life
    /// of the gateway.
    /// </summary>
    public bool Pinned { get; set; }
}

/// <summary>Lightweight view of a running backend for the /admin/status endpoint.</summary>
public sealed record BackendStatus(string Version, int HostPort, bool Healthy, DateTimeOffset LastUsedUtc);
