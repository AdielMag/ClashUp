using System.Reflection;

namespace ClashUp.Server.Common;

/// <summary>
/// The server's semantic version, read once from the entry assembly's
/// <see cref="AssemblyInformationalVersionAttribute"/>. The value is injected at
/// build time from Directory.Build.props (overridden by CI via
/// <c>-p:InformationalVersion=x.y.z</c>). Every tier keys version-aware behaviour
/// (gateway routing, GS registration, heartbeats) on this single source.
/// </summary>
public static class ServerVersion
{
    /// <summary>The current server version, e.g. "1.2.0". Falls back to "0.0.0" if unset.</summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ServerVersion).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        // Strip any source-revision suffix the SDK appends (e.g. "1.2.0+abc123").
        var plus = informational.IndexOf('+');
        return plus >= 0 ? informational[..plus] : informational;
    }
}
