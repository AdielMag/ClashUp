namespace ClashUp.Server.Gateway;

/// <summary>
/// Configures one gateway instance for a single tier. The same gateway image is
/// deployed to both the Services and GameServer MIGs, differing only by these
/// values (bound from the <c>Gateway</c> config section / env vars).
/// </summary>
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>"Services" or "GameServer" — informational, used in logs and /admin/status.</summary>
    public string Tier { get; init; } = "GameServer";

    /// <summary>Public gRPC (HTTP/2) port clients connect to. 5001 for Services, 5101 for GameServer.</summary>
    public int ListenPort { get; init; } = 5101;

    /// <summary>Plain-HTTP port serving /healthz and /admin/status for the LB/MIG and the dashboard.</summary>
    public int AdminPort { get; init; } = 9101;

    /// <summary>The port the backend container image listens on (its ASPNETCORE_URLS port).</summary>
    public int BackendPort { get; init; } = 5101;

    /// <summary>
    /// Fully-qualified image repository WITHOUT a tag, e.g.
    /// <c>us-central1-docker.pkg.dev/my-proj/clashup-docker/clashup-gameserver</c>.
    /// The requested client version becomes the tag.
    /// </summary>
    public string ImageRepository { get; init; } = string.Empty;

    /// <summary>Host the backends' published ports live on (loopback when the gateway shares the host network).</summary>
    public string BackendHost { get; init; } = "127.0.0.1";

    /// <summary>
    /// Tag used for requests without an <c>x-client-version</c> header — i.e.
    /// internal server-to-server traffic. Defaults to "latest" (always published
    /// by CI). Real clients always send their version, so an explicit unknown
    /// version still yields the upgrade-required response.
    /// </summary>
    public string DefaultVersion { get; init; } = "latest";

    /// <summary>Stop a version's backend after it has been idle this long. 0 disables idle eviction.</summary>
    public int IdleVersionTtlMinutes { get; init; } = 30;

    /// <summary>How long to wait for a freshly-spawned backend's /healthz to go green.</summary>
    public int BackendHealthTimeoutSeconds { get; init; } = 60;

    /// <summary>How often the maintenance loop health-checks backends and evicts idle ones.</summary>
    public int MaintenanceIntervalSeconds { get; init; } = 30;

    /// <summary>How often host memory utilization is pushed to Cloud Monitoring, in seconds.</summary>
    public int MetricsIntervalSeconds { get; init; } = 30;

    /// <summary>Versions to spawn eagerly at startup so common clients are never cold.</summary>
    public string[] PrewarmVersions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Environment variables injected into every backend container, each as a
    /// literal "KEY=VALUE" string (e.g. "Mongo__ConnectionString=mongodb+srv://…").
    /// A list of strings — not a dictionary — so backend var names containing
    /// "__" don't collide with the configuration hierarchy delimiter.
    /// </summary>
    public string[] BackendEnvironment { get; init; } = Array.Empty<string>();
}
