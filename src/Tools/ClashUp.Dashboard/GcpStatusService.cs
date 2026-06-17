using Google.Cloud.ArtifactRegistry.V1;
using Google.Cloud.Compute.V1;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;

namespace ClashUp.Tools.Dashboard;

/// <summary>
/// Aggregates the fleet view from GCP read APIs: running instances + state
/// (Compute), CCU/CPU/RAM (Cloud Monitoring), and available image tags
/// (Artifact Registry). Every section degrades independently — a failure in one
/// becomes an entry in <see cref="FleetStatus.Errors"/> rather than a 500.
/// </summary>
public sealed class GcpStatusService
{
    private const string CcuMetric = "custom.googleapis.com/gameserver/ccu";
    private const string CpuMetric = "compute.googleapis.com/instance/cpu/utilization";
    private const string RamMetric = "custom.googleapis.com/instance/memory_utilization";

    private readonly DashboardOptions _options;
    private readonly ILogger<GcpStatusService> _logger;

    private readonly Lazy<Task<RegionInstanceGroupManagersClient>> _migClient;
    private readonly Lazy<Task<MetricServiceClient>> _metricClient;
    private readonly Lazy<Task<ArtifactRegistryClient>> _registryClient;

    public GcpStatusService(IOptions<DashboardOptions> options, ILogger<GcpStatusService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Credentials come from Application Default Credentials. When a key path
        // is configured, Program.cs exports it via GOOGLE_APPLICATION_CREDENTIALS
        // before this service is constructed, so the default builders pick it up.
        _migClient = new(() => new RegionInstanceGroupManagersClientBuilder().BuildAsync());
        _metricClient = new(() => new MetricServiceClientBuilder().BuildAsync());
        _registryClient = new(() => new ArtifactRegistryClientBuilder().BuildAsync());
    }

    public async Task<FleetStatus> GetFleetStatusAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var ccu = await SafeAsync(() => QueryCcuAsync(cancellationToken), errors, "CCU")
            ?? new Dictionary<string, List<VersionCcu>>();
        var cpu = await SafeAsync(() => QueryGaugeByInstanceAsync(CpuMetric, null, v => v * 100, cancellationToken), errors, "CPU")
            ?? new Dictionary<string, double>();
        var ram = await SafeAsync(() => QueryGaugeByInstanceAsync(RamMetric, null, v => v, cancellationToken), errors, "RAM")
            ?? new Dictionary<string, double>();

        var tiers = new List<TierStatus>
        {
            await BuildTierAsync("Services", _options.ServicesMig, cpu, ram, ccu, errors, cancellationToken),
            await BuildTierAsync("GameServer", _options.GameServerMig, cpu, ram, ccu, errors, cancellationToken),
        };

        var images = await SafeAsync(() => ListAvailableImagesAsync(cancellationToken), errors, "Artifact Registry")
            ?? new List<ImageVersions>();

        return new FleetStatus(DateTimeOffset.UtcNow, tiers, images, errors);
    }

    private async Task<TierStatus> BuildTierAsync(
        string tier,
        string migName,
        IReadOnlyDictionary<string, double> cpu,
        IReadOnlyDictionary<string, double> ram,
        IReadOnlyDictionary<string, List<VersionCcu>> ccu,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var instances = new List<InstanceStatus>();
        try
        {
            var client = await _migClient.Value;
            await foreach (var mi in client.ListManagedInstancesAsync(_options.ProjectId, _options.Region, migName)
                               .WithCancellation(cancellationToken))
            {
                var id = mi.HasId ? mi.Id.ToString() : string.Empty;
                instances.Add(new InstanceStatus(
                    Name: LastSegment(mi.Instance),
                    Id: id,
                    State: mi.InstanceStatus ?? mi.CurrentAction ?? "UNKNOWN",
                    CpuPercent: cpu.TryGetValue(id, out var c) ? Math.Round(c, 1) : null,
                    RamPercent: ram.TryGetValue(id, out var r) ? Math.Round(r, 1) : null,
                    Versions: ccu.TryGetValue(id, out var v) ? v : Array.Empty<VersionCcu>()));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list instances for {Mig}", migName);
            errors.Add($"{tier} instances: {ex.Message}");
        }

        return new TierStatus(tier, instances);
    }

    private async Task<Dictionary<string, List<VersionCcu>>> QueryCcuAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<VersionCcu>>(StringComparer.Ordinal);
        await foreach (var series in ListSeriesAsync($"metric.type = \"{CcuMetric}\"", cancellationToken))
        {
            if (series.Points.Count == 0)
            {
                continue;
            }

            var instanceId = Label(series.Resource.Labels, "instance_id");
            var version = Label(series.Metric.Labels, "version", "unknown");
            var ccu = series.Points[0].Value.Int64Value;

            if (!result.TryGetValue(instanceId, out var list))
            {
                result[instanceId] = list = new List<VersionCcu>();
            }

            list.Add(new VersionCcu(version, ccu));
        }

        return result;
    }

    private async Task<Dictionary<string, double>> QueryGaugeByInstanceAsync(
        string metricType, string? extraFilter, Func<double, double> transform, CancellationToken cancellationToken)
    {
        var filter = $"metric.type = \"{metricType}\"";
        if (!string.IsNullOrEmpty(extraFilter))
        {
            filter += $" AND {extraFilter}";
        }

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        await foreach (var series in ListSeriesAsync(filter, cancellationToken))
        {
            if (series.Points.Count == 0)
            {
                continue;
            }

            var instanceId = Label(series.Resource.Labels, "instance_id");
            if (!string.IsNullOrEmpty(instanceId))
            {
                result[instanceId] = transform(series.Points[0].Value.DoubleValue);
            }
        }

        return result;
    }

    private async IAsyncEnumerable<TimeSeries> ListSeriesAsync(
        string filter, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = await _metricClient.Value;
        var request = new ListTimeSeriesRequest
        {
            ProjectName = new Google.Api.Gax.ResourceNames.ProjectName(_options.ProjectId),
            Filter = filter,
            Interval = new TimeInterval
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5)),
                EndTime = Timestamp.FromDateTime(DateTime.UtcNow),
            },
            View = ListTimeSeriesRequest.Types.TimeSeriesView.Full,
        };

        await foreach (var series in client.ListTimeSeriesAsync(request).WithCancellation(cancellationToken))
        {
            yield return series;
        }
    }

    private async Task<List<ImageVersions>> ListAvailableImagesAsync(CancellationToken cancellationToken)
    {
        var client = await _registryClient.Value;
        var parent = $"projects/{_options.ProjectId}/locations/{_options.Region}/repositories/{_options.Repository}";

        var tagsByImage = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        await foreach (var image in client.ListDockerImagesAsync(parent).WithCancellation(cancellationToken))
        {
            var imageName = ParseImageName(image.Name);
            if (string.IsNullOrEmpty(imageName) || image.Tags.Count == 0)
            {
                continue;
            }

            if (!tagsByImage.TryGetValue(imageName, out var tags))
            {
                tagsByImage[imageName] = tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var tag in image.Tags)
            {
                tags.Add(tag);
            }
        }

        return tagsByImage
            .Select(kvp => new ImageVersions(kvp.Key, kvp.Value.ToArray()))
            .OrderBy(i => i.Image, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<T?> SafeAsync<T>(Func<Task<T>> action, List<string> errors, string label) where T : class
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Label} query failed", label);
            errors.Add($"{label}: {ex.Message}");
            return null;
        }
    }

    private static string Label(IDictionary<string, string> labels, string key, string fallback = "") =>
        labels.TryGetValue(key, out var value) ? value : fallback;

    private static string LastSegment(string? url) =>
        string.IsNullOrEmpty(url) ? string.Empty : url[(url.LastIndexOf('/') + 1)..];

    private static string ParseImageName(string resourceName)
    {
        // .../dockerImages/<image>@sha256:... or .../dockerImages/<image>:<tag>
        const string marker = "dockerImages/";
        var idx = resourceName.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return string.Empty;
        }

        var rest = resourceName[(idx + marker.Length)..];
        var cut = rest.IndexOfAny(new[] { '@', ':' });
        return cut >= 0 ? rest[..cut] : rest;
    }
}
