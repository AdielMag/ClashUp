namespace ClashUp.Server.Gateway.Supervisor;

/// <summary>
/// Owns the per-version backend containers on this instance: spawns them on
/// demand (pulling the image from the registry), health-checks and restarts
/// them, and evicts idle ones. The gateway forwarder asks it which local port
/// serves a given client version.
/// </summary>
public interface IProcessSupervisor : IAsyncDisposable
{
    /// <summary>
    /// Ensure a healthy backend for <paramref name="version"/> is running and
    /// return its loopback host port. Spawns one (pull + run + wait-for-health)
    /// if needed. Throws <see cref="VersionUnavailableException"/> when no image
    /// exists for the version.
    /// </summary>
    Task<int> EnsureVersionAsync(string version, CancellationToken cancellationToken);

    /// <summary>Versions with a backend currently registered (healthy or not).</summary>
    IReadOnlyCollection<string> RunningVersions { get; }

    /// <summary>Snapshot of all managed backends for diagnostics.</summary>
    IReadOnlyList<BackendStatus> GetStatus();

    /// <summary>Health-check backends (restart crashed) and stop idle ones. Called on a timer.</summary>
    Task RunMaintenanceAsync(CancellationToken cancellationToken);

    /// <summary>Eagerly spawn the configured pre-warm versions.</summary>
    Task PrewarmAsync(CancellationToken cancellationToken);
}
