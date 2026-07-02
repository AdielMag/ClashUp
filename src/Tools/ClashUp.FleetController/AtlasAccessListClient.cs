using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ClashUp.Tools.FleetController;

/// <summary>
/// Minimal MongoDB Atlas Admin API client for the project Network Access list.
/// Used to keep the (re-allocated-each-wake) Cloud NAT egress IP allowlisted so
/// Services instances can reach the Atlas cluster.
///
/// Auth is HTTP Digest with an Atlas API key pair (public = username, private =
/// password). The API is versioned via a media-type Accept header.
/// </summary>
public sealed class AtlasAccessListClient
{
    private const string BaseUrl = "https://cloud.mongodb.com/api/atlas/v2";
    private const string MediaType = "application/vnd.atlas.2023-11-15+json";

    private readonly FleetControllerOptions _o;
    private readonly ILogger<AtlasAccessListClient> _logger;
    private readonly Lazy<HttpClient> _http;

    public AtlasAccessListClient(IOptions<FleetControllerOptions> options, ILogger<AtlasAccessListClient> logger)
    {
        _o = options.Value;
        _logger = logger;
        _http = new Lazy<HttpClient>(BuildClient);
    }

    /// <summary>True only when the Atlas keys + project id are all configured.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_o.AtlasPublicKey) &&
        !string.IsNullOrWhiteSpace(_o.AtlasPrivateKey) &&
        !string.IsNullOrWhiteSpace(_o.AtlasProjectId);

    private HttpClient BuildClient()
    {
        // HttpClientHandler performs the Digest challenge/response using these creds.
        var handler = new HttpClientHandler
        {
            Credentials = new NetworkCredential(_o.AtlasPublicKey, _o.AtlasPrivateKey),
            PreAuthenticate = false,
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl + "/") };
        client.DefaultRequestHeaders.Accept.ParseAdd(MediaType);
        return client;
    }

    /// <summary>
    /// Ensure <paramref name="ipAddress"/> is the only allowlist entry we own: add it,
    /// then remove any prior entries stamped with our comment that aren't this IP.
    /// </summary>
    public async Task EnsureOnlyAsync(string ipAddress, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Atlas keys not configured — skipping allowlist update for {Ip}", ipAddress);
            return;
        }

        // Add first (never leave a gap where the NAT IP is un-allowlisted).
        await AddAsync(ipAddress, ct);
        await PruneStaleAsync(ipAddress, ct);
    }

    private async Task AddAsync(string ipAddress, CancellationToken ct)
    {
        // POST accepts an array; Atlas is idempotent on a duplicate ipAddress.
        var body = new[] { new { ipAddress, comment = _o.AtlasEntryComment } };
        using var resp = await _http.Value.PostAsJsonAsync(
            $"groups/{_o.AtlasProjectId}/accessList", body, ct);

        if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.Conflict)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Atlas accessList add failed ({(int)resp.StatusCode}): {detail}");
        }
        _logger.LogInformation("Atlas allowlist: added {Ip}", ipAddress);
    }

    private async Task PruneStaleAsync(string keepIp, CancellationToken ct)
    {
        using var listResp = await _http.Value.GetAsync($"groups/{_o.AtlasProjectId}/accessList", ct);
        listResp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("results", out var results))
        {
            return;
        }

        foreach (var entry in results.EnumerateArray())
        {
            var comment = entry.TryGetProperty("comment", out var c) ? c.GetString() : null;
            var ip = entry.TryGetProperty("ipAddress", out var i) ? i.GetString() : null;

            // Only ever touch entries WE stamped, and never the one we just added.
            if (comment != _o.AtlasEntryComment || string.IsNullOrEmpty(ip) || ip == keepIp)
            {
                continue;
            }

            using var del = await _http.Value.DeleteAsync(
                $"groups/{_o.AtlasProjectId}/accessList/{Uri.EscapeDataString(ip)}", ct);
            if (del.IsSuccessStatusCode || del.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Atlas allowlist: pruned stale {Ip}", ip);
            }
            else
            {
                // A prune failure is non-fatal (stale entry just lingers) — log and move on.
                _logger.LogWarning("Atlas allowlist: failed to prune {Ip} ({Status})", ip, del.StatusCode);
            }
        }
    }
}
