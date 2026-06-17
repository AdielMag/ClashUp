namespace ClashUp.Server.Gateway.Supervisor;

/// <summary>
/// Thrown when a requested client version has no corresponding image in the
/// registry (the tag does not exist). The gateway translates this into a gRPC
/// <c>FAILED_PRECONDITION</c> + <c>required-action: upgrade-client</c> response.
/// </summary>
public sealed class VersionUnavailableException : Exception
{
    public VersionUnavailableException(string version)
        : base($"No server image is available for client version '{version}'.")
    {
        Version = version;
    }

    public string Version { get; }
}
