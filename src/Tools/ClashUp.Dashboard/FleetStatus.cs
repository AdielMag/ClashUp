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

/// <summary>
/// Per-day "awake hours" of the fleet over a date range, for the uptime calendar.
/// Derived from the presence of <c>compute.googleapis.com/instance/uptime</c> samples:
/// the auto-sleep fleet scales to 0 when idle, so an hour with any instance reporting
/// uptime is an hour the fleet (and the billing meter) was awake. Days are UTC.
/// </summary>
public sealed record UptimeCalendar(DateOnly From, DateOnly To, IReadOnlyList<UptimeDay> Days);

/// <summary><c>AwakeHours</c> is 0–24 — whole hours within the day that had any running instance.</summary>
public sealed record UptimeDay(DateOnly Date, int AwakeHours);
