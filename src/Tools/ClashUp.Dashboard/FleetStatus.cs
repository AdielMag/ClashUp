namespace ClashUp.Tools.Dashboard;

/// <summary>The full snapshot returned by <c>GET /api/status</c>.</summary>
public sealed record FleetStatus(
    DateTimeOffset GeneratedAt,
    bool Asleep,
    IdleCheckStatus? IdleCheck,
    IReadOnlyList<TierStatus> Tiers,
    IReadOnlyList<ImageVersions> AvailableImages,
    IReadOnlyList<string> Errors);

/// <summary>
/// State of the Cloud Scheduler idle-check job. <c>State</c> is "Enabled" while the
/// fleet is awake (and counting down to the next check) or "Paused" once it has slept.
/// </summary>
public sealed record IdleCheckStatus(string State, DateTimeOffset? NextRunUtc);

public sealed record TierStatus(string Tier, IReadOnlyList<InstanceStatus> Instances);

public sealed record InstanceStatus(
    string Name,
    string Id,
    string State,
    double? CpuPercent,
    double? RamPercent,
    IReadOnlyList<VersionCcu> Versions);

public sealed record VersionCcu(string Version, long Ccu);

public sealed record ImageVersions(string Image, IReadOnlyList<string> Tags);
