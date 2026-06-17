using System.Net.Http;
using System.Text.Json;

namespace ClashUp.Server.Common.Gce;

/// <summary>
/// Reads instance facts from the GCE metadata server
/// (<c>http://metadata.google.internal</c>). Every call has a short timeout and
/// returns <c>null</c> when the metadata server is unreachable, so local/dev
/// hosts (where there is no metadata server) degrade gracefully rather than
/// blocking startup.
/// </summary>
public static class GceMetadataHelper
{
    private const string MetadataBase = "http://metadata.google.internal/computeMetadata/v1";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <summary>The instance's external (public) IPv4 address, or null if none / not on GCE.</summary>
    public static Task<string?> GetExternalIpAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("/instance/network-interfaces/0/access-configs/0/external-ip", cancellationToken);

    /// <summary>The numeric GCE instance id, or null if not on GCE.</summary>
    public static Task<string?> GetInstanceIdAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("/instance/id", cancellationToken);

    /// <summary>The instance name, or null if not on GCE.</summary>
    public static Task<string?> GetInstanceNameAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("/instance/name", cancellationToken);

    /// <summary>The short zone name (e.g. "us-central1-a"), or null if not on GCE.</summary>
    public static async Task<string?> GetZoneAsync(CancellationToken cancellationToken = default)
    {
        // The metadata value is "projects/<num>/zones/<zone>" — keep only the zone.
        var raw = await FetchAsync("/instance/zone", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var slash = raw.LastIndexOf('/');
        return slash >= 0 ? raw[(slash + 1)..] : raw;
    }

    /// <summary>The GCP project id this instance runs in, or null if not on GCE.</summary>
    public static Task<string?> GetProjectIdAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("/project/project-id", cancellationToken);

    /// <summary>True when the metadata server responds — i.e. we are running on GCE.</summary>
    public static async Task<bool> IsRunningOnGceAsync(CancellationToken cancellationToken = default) =>
        await GetInstanceIdAsync(cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// An OAuth2 access token for the instance's default service account, or null
    /// off-GCE. Used as the password (username <c>oauth2accesstoken</c>) when
    /// pulling private images from Artifact Registry via the Docker Engine API.
    /// </summary>
    public static async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var json = await FetchAsync("/instance/service-accounts/default/token", cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("access_token", out var token)
                ? token.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> FetchAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = Timeout };
            using var request = new HttpRequestMessage(HttpMethod.Get, MetadataBase + path);
            request.Headers.Add("Metadata-Flavor", "Google");

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var value = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            // Not on GCE, DNS failure, or timeout — treat as "no metadata available".
            return null;
        }
    }
}
