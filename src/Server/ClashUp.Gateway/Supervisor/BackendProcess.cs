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
}

/// <summary>Lightweight view of a running backend for the /admin/status endpoint.</summary>
public sealed record BackendStatus(string Version, int HostPort, bool Healthy, DateTimeOffset LastUsedUtc);
