using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ClashUp.Server.Common.Gce;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.Gateway.Supervisor;

/// <summary>
/// Docker-backed <see cref="IProcessSupervisor"/>. Runs one backend container per
/// client version on the local Docker daemon (socket mounted into the gateway),
/// pulling images from Artifact Registry on demand.
/// </summary>
public sealed class ProcessSupervisor : IProcessSupervisor
{
    private readonly GatewayOptions _options;
    private readonly ILogger<ProcessSupervisor> _logger;

    private readonly ConcurrentDictionary<string, BackendProcess> _backends = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _versionLocks = new(StringComparer.Ordinal);

    private readonly Lazy<DockerClient> _docker;
    private readonly HttpClient _healthClient;

    public ProcessSupervisor(IOptions<GatewayOptions> options, ILogger<ProcessSupervisor> logger)
    {
        _options = options.Value;
        _logger = logger;
        _docker = new Lazy<DockerClient>(() => new DockerClientConfiguration().CreateClient());

        // Backends speak HTTP/2 cleartext (h2c) only, so the health probe must too.
        _healthClient = new HttpClient(new SocketsHttpHandler())
        {
            Timeout = TimeSpan.FromSeconds(3),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };
    }

    public IReadOnlyCollection<string> RunningVersions => _backends.Keys.ToArray();

    public IReadOnlyList<BackendStatus> GetStatus() =>
        _backends.Values
            .Select(b => new BackendStatus(b.Version, b.HostPort, b.Healthy, b.LastUsedUtc))
            .ToArray();

    public async Task<int> EnsureVersionAsync(string version, CancellationToken cancellationToken)
    {
        version = NormalizeVersion(version);

        if (_backends.TryGetValue(version, out var existing) && existing.Healthy)
        {
            existing.LastUsedUtc = DateTimeOffset.UtcNow;
            return existing.HostPort;
        }

        var gate = _versionLocks.GetOrAdd(version, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the per-version lock.
            if (_backends.TryGetValue(version, out existing) && existing.Healthy)
            {
                existing.LastUsedUtc = DateTimeOffset.UtcNow;
                return existing.HostPort;
            }

            var spawned = await SpawnAsync(version, cancellationToken).ConfigureAwait(false);
            _backends[version] = spawned;
            return spawned.HostPort;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<BackendProcess> SpawnAsync(string version, CancellationToken cancellationToken)
    {
        var imageRef = $"{_options.ImageRepository}:{version}";

        await EnsureImageAsync(imageRef, version, cancellationToken).ConfigureAwait(false);

        var hostPort = GetFreeLoopbackPort();
        var containerId = await CreateAndStartAsync(imageRef, version, hostPort, cancellationToken)
            .ConfigureAwait(false);

        var healthy = await WaitForHealthyAsync(hostPort, cancellationToken).ConfigureAwait(false);
        if (!healthy)
        {
            _logger.LogError("Backend {Version} failed to become healthy; tearing it down.", version);
            await TryStopAndRemoveAsync(containerId).ConfigureAwait(false);
            throw new InvalidOperationException($"Backend for version '{version}' never reported healthy.");
        }

        _logger.LogInformation(
            "Backend {Version} ready on 127.0.0.1:{Port} (container {Container}).",
            version, hostPort, containerId[..Math.Min(12, containerId.Length)]);

        return new BackendProcess
        {
            Version = version,
            ContainerId = containerId,
            HostPort = hostPort,
            Healthy = true,
        };
    }

    private async Task EnsureImageAsync(string imageRef, string version, CancellationToken cancellationToken)
    {
        // Skip the pull if the image is already present locally (supports
        // locally-built images during dev with no registry).
        try
        {
            await _docker.Value.Images.InspectImageAsync(imageRef, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (DockerImageNotFoundException)
        {
            // Not local — fall through to pull.
        }

        var (repository, tag) = SplitRepositoryAndTag(imageRef);
        var auth = await GetRegistryAuthAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _docker.Value.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = repository, Tag = tag },
                auth,
                new Progress<JSONMessage>(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DockerApiException ex) when (IsImageNotFound(ex))
        {
            _logger.LogWarning("No image for version {Version} ({ImageRef}) — rejecting as upgrade-required.", version, imageRef);
            throw new VersionUnavailableException(version);
        }
    }

    private async Task<string> CreateAndStartAsync(
        string imageRef, string version, int hostPort, CancellationToken cancellationToken)
    {
        var portKey = $"{_options.BackendPort}/tcp";
        var create = await _docker.Value.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = imageRef,
                Name = $"clashup-{_options.Tier.ToLowerInvariant()}-{SanitizeForName(version)}-{Guid.NewGuid():N}"[..40],
                Env = BuildBackendEnvironment(),
                ExposedPorts = new Dictionary<string, EmptyStruct> { [portKey] = default },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [portKey] = new List<PortBinding>
                        {
                            new() { HostIP = "127.0.0.1", HostPort = hostPort.ToString() },
                        },
                    },
                    RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.No },
                },
            },
            cancellationToken).ConfigureAwait(false);

        await _docker.Value.Containers.StartContainerAsync(
            create.ID, new ContainerStartParameters(), cancellationToken).ConfigureAwait(false);

        return create.ID;
    }

    private IList<string> BuildBackendEnvironment()
    {
        // Configured "KEY=VALUE" entries, plus the listen URL the backend must bind.
        var env = _options.BackendEnvironment
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToList();

        env.RemoveAll(e => e.StartsWith("ASPNETCORE_URLS=", StringComparison.Ordinal));
        env.Add($"ASPNETCORE_URLS=http://0.0.0.0:{_options.BackendPort}");

        if (!env.Any(e => e.StartsWith("ASPNETCORE_ENVIRONMENT=", StringComparison.Ordinal)))
        {
            env.Add("ASPNETCORE_ENVIRONMENT=Production");
        }

        // Disable the .NET write-xor-execute JIT feature. On newer host kernels
        // (COS moved to 6.6.x) it triggers a general-protection-fault crash in the
        // backend's dotnet process on startup (preceded by a "memfd_create() called
        // without MFD_EXEC or MFD_NOEXEC_SEAL set" warning) → the backend never
        // reports healthy and the prewarm/on-demand spawn fails with gs_provision_failed.
        // Setting this to 0 is the standard runtime workaround and is kernel/COS-agnostic.
        if (!env.Any(e => e.StartsWith("DOTNET_EnableWriteXorExecute=", StringComparison.Ordinal)))
        {
            env.Add("DOTNET_EnableWriteXorExecute=0");
        }

        return env;
    }

    private async Task<bool> WaitForHealthyAsync(int hostPort, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(5, _options.BackendHealthTimeoutSeconds));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await ProbeHealthAsync(hostPort, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private async Task<bool> ProbeHealthAsync(int hostPort, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _healthClient
                .GetAsync($"http://{_options.BackendHost}:{hostPort}/healthz", cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task PrewarmAsync(CancellationToken cancellationToken)
    {
        var versions = new HashSet<string>(
            _options.PrewarmVersions.Where(v => !string.IsNullOrWhiteSpace(v)),
            StringComparer.Ordinal);

        if (_options.PrewarmDiscoveredVersions)
        {
            // Prewarm ONLY the newest published version. One warm backend is enough
            // to register the instance with Services (the bootstrap requirement);
            // older clients still spawn their version on demand. Prewarming every
            // discovered tag would resurrect retired versions on each fresh VM until
            // their images are deleted — so we deliberately don't.
            var newest = SelectNewestVersion(
                await DiscoverVersionTagsAsync(cancellationToken).ConfigureAwait(false));
            if (newest is not null)
            {
                versions.Add(newest);
            }
        }

        foreach (var version in versions)
        {
            try
            {
                await EnsureVersionAsync(version, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pre-warm of version {Version} failed", version);
            }
        }
    }

    /// <summary>
    /// Lists the published image tags in the configured repository via the Docker
    /// Registry v2 API, keeping only real version tags (drops "latest" and the
    /// default dev tag). Used by the GameServer tier to bootstrap: prewarming each
    /// published version starts a backend that registers the instance with
    /// Services. Returns empty on any failure (off-GCE, no token, transient) — the
    /// caller still honours the explicit PrewarmVersions list.
    /// </summary>
    private async Task<IReadOnlyList<string>> DiscoverVersionTagsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var registryHost = _options.ImageRepository.Split('/', 2)[0];
            var repoPath = _options.ImageRepository[(registryHost.Length + 1)..];
            var token = await GceMetadataHelper.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("No registry token (off-GCE?) — skipping version discovery.");
                return Array.Empty<string>();
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"https://{registryHost}/v2/{repoPath}/tags/list");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tag list request failed ({Status}) — skipping version discovery.", response.StatusCode);
                return Array.Empty<string>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tags", out var tagsElement)
                || tagsElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var tags = tagsElement.EnumerateArray()
                .Select(t => t.GetString())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .Where(IsVersionTag)
                .ToArray();

            _logger.LogInformation("Discovered {Count} version tag(s) to prewarm: {Tags}", tags.Length, string.Join(", ", tags));
            return tags;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Version discovery failed — skipping (explicit PrewarmVersions still apply).");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Picks the highest semantic version from a set of release tags. Tags that
    /// parse as <see cref="Version"/> (e.g. "1.2.0") sort numerically and outrank
    /// any non-parsing tags, which fall back to ordinal comparison.
    /// </summary>
    private static string? SelectNewestVersion(IReadOnlyList<string> tags) =>
        tags
            .OrderByDescending(t => System.Version.TryParse(t, out var v) ? v : null)
            .ThenByDescending(t => t, StringComparer.Ordinal)
            .FirstOrDefault();

    // A real release tag, not a moving/dev pointer. CI publishes semver tags
    // (e.g. "1.2.0") plus "latest"; the dev fallback is "0.0.1".
    private static bool IsVersionTag(string tag) =>
        !string.Equals(tag, "latest", StringComparison.OrdinalIgnoreCase)
        && tag != "0.0.1"
        && tag.Length > 0
        && char.IsDigit(tag[0]);

    public async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        var idleCutoff = _options.IdleVersionTtlMinutes > 0
            ? DateTimeOffset.UtcNow.AddMinutes(-_options.IdleVersionTtlMinutes)
            : (DateTimeOffset?)null;

        foreach (var backend in _backends.Values.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Evict idle backends — the next request will respawn on demand.
            // BUT never evict the last remaining backend: on the GameServer tier it
            // holds the instance's registration + heartbeat with Services (and marks
            // itself Draining on SIGTERM), so dropping to zero backends would pull the
            // instance out of the matchmaker's healthy set and break matchmaking once
            // the fleet goes idle. Keeping exactly one alive costs ~one idle process
            // and guarantees the instance stays matchable. (_backends.Count is live —
            // it drops as we remove, so this naturally stops at one.)
            if (idleCutoff is { } cutoff && backend.LastUsedUtc < cutoff && _backends.Count > 1)
            {
                _logger.LogInformation("Stopping idle backend {Version} (idle since {LastUsed:o}).", backend.Version, backend.LastUsedUtc);
                await RemoveBackendAsync(backend).ConfigureAwait(false);
                continue;
            }

            var healthy = await ProbeHealthAsync(backend.HostPort, cancellationToken).ConfigureAwait(false);
            backend.Healthy = healthy;
            if (healthy)
            {
                continue;
            }

            // Unhealthy: drop it only if the container is no longer running, so a
            // request can respawn a clean one. (A still-running-but-slow backend
            // is left alone for the next cycle.)
            if (!await IsContainerRunningAsync(backend.ContainerId, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning("Backend {Version} container is not running — removing.", backend.Version);
                await RemoveBackendAsync(backend).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> IsContainerRunningAsync(string containerId, CancellationToken cancellationToken)
    {
        try
        {
            var inspect = await _docker.Value.Containers
                .InspectContainerAsync(containerId, cancellationToken).ConfigureAwait(false);
            return inspect.State.Running;
        }
        catch
        {
            return false;
        }
    }

    private async Task RemoveBackendAsync(BackendProcess backend)
    {
        _backends.TryRemove(backend.Version, out _);
        await TryStopAndRemoveAsync(backend.ContainerId).ConfigureAwait(false);
    }

    private async Task TryStopAndRemoveAsync(string containerId)
    {
        try
        {
            await _docker.Value.Containers.StopContainerAsync(
                containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stop of container {Container} failed (may already be stopped).", containerId);
        }

        try
        {
            await _docker.Value.Containers.RemoveContainerAsync(
                containerId, new ContainerRemoveParameters { Force = true }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Remove of container {Container} failed.", containerId);
        }
    }

    private async Task<AuthConfig?> GetRegistryAuthAsync(CancellationToken cancellationToken)
    {
        var registryHost = _options.ImageRepository.Split('/', 2)[0];
        var isGoogleRegistry = registryHost.EndsWith("docker.pkg.dev", StringComparison.OrdinalIgnoreCase)
            || registryHost.EndsWith("gcr.io", StringComparison.OrdinalIgnoreCase);
        if (!isGoogleRegistry)
        {
            return null;
        }

        var token = await GceMetadataHelper.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            return null; // Off GCE — rely on a locally-present image or anonymous pull.
        }

        return new AuthConfig
        {
            Username = "oauth2accesstoken",
            Password = token,
            ServerAddress = registryHost,
        };
    }

    private static (string Repository, string Tag) SplitRepositoryAndTag(string imageRef)
    {
        var lastColon = imageRef.LastIndexOf(':');
        var lastSlash = imageRef.LastIndexOf('/');
        return lastColon > lastSlash
            ? (imageRef[..lastColon], imageRef[(lastColon + 1)..])
            : (imageRef, "latest");
    }

    private static bool IsImageNotFound(DockerApiException ex) =>
        ex.StatusCode == HttpStatusCode.NotFound
        || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("manifest unknown", StringComparison.OrdinalIgnoreCase);

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string NormalizeVersion(string version)
    {
        version = version?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(version))
        {
            throw new VersionUnavailableException(string.Empty);
        }

        return version;
    }

    private static string SanitizeForName(string version) =>
        new(version.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    public async ValueTask DisposeAsync()
    {
        foreach (var backend in _backends.Values.ToArray())
        {
            await TryStopAndRemoveAsync(backend.ContainerId).ConfigureAwait(false);
        }

        _backends.Clear();
        _healthClient.Dispose();
        if (_docker.IsValueCreated)
        {
            _docker.Value.Dispose();
        }
    }
}
