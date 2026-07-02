using System.Net.Http.Headers;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.ArtifactRegistry.V1;
using Google.Cloud.Compute.V1;
using Google.Cloud.Monitoring.V3;
using Google.Cloud.Scheduler.V1;
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
    private const string GameServerCcuMetric = "custom.googleapis.com/gameserver/ccu";
    private const string ServicesCcuMetric = "custom.googleapis.com/services/ccu";
    private const string CpuMetric = "compute.googleapis.com/instance/cpu/utilization";
    private const string RamMetric = "custom.googleapis.com/instance/memory_utilization";
    private const string UptimeMetric = "compute.googleapis.com/instance/uptime";

    private readonly DashboardOptions _options;
    private readonly ILogger<GcpStatusService> _logger;

    private readonly Lazy<Task<RegionInstanceGroupManagersClient>> _migClient;
    private readonly Lazy<Task<MetricServiceClient>> _metricClient;
    private readonly Lazy<Task<ArtifactRegistryClient>> _registryClient;
    private readonly Lazy<Task<CloudSchedulerClient>> _schedulerClient;

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
        _schedulerClient = new(() => new CloudSchedulerClientBuilder().BuildAsync());
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

        bool asleep = false;
        try
        {
            asleep = await QueryAsleepAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fleet state query failed");
            errors.Add($"Fleet state: {ex.Message}");
        }

        var idleCheck = await SafeAsync(() => QueryIdleCheckAsync(cancellationToken), errors, "Idle check");

        return new FleetStatus(DateTimeOffset.UtcNow, asleep, idleCheck, tiers, images, errors);
    }

    /// <summary>
    /// Builds the uptime calendar over <paramref name="from"/>..<paramref name="to"/> (inclusive, UTC days).
    /// "Uptime" here is awake-time: the fleet auto-sleeps to 0 instances when idle, so we count, per day,
    /// the whole hours in which any instance reported <c>instance/uptime</c> — i.e. the hours the fleet was
    /// running and billable. We align the metric into 1-hour buckets and collapse all instances into one
    /// series; a bucket with a positive sample is one awake hour, attributed to its UTC day.
    /// Note: Cloud Monitoring retains this metric for ~6 weeks, so older days come back as 0 (no data).
    /// </summary>
    public async Task<UptimeCalendar> GetUptimeCalendarAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var startUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); // exclusive end → include 'to'

        var client = await _metricClient.Value;
        var request = new ListTimeSeriesRequest
        {
            ProjectName = new Google.Api.Gax.ResourceNames.ProjectName(_options.ProjectId),
            Filter = $"metric.type = \"{UptimeMetric}\" AND resource.type = \"gce_instance\"",
            Interval = new TimeInterval
            {
                StartTime = Timestamp.FromDateTime(startUtc),
                EndTime = Timestamp.FromDateTime(endUtc),
            },
            // Per-instance: count samples in each hour (>0 ⇒ that instance was up that hour).
            // Cross-series SUM collapses all instances into one fleet-wide series; we only read presence.
            Aggregation = new Aggregation
            {
                AlignmentPeriod = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(TimeSpan.FromHours(1)),
                PerSeriesAligner = Aggregation.Types.Aligner.AlignCount,
                CrossSeriesReducer = Aggregation.Types.Reducer.ReduceSum,
            },
            View = ListTimeSeriesRequest.Types.TimeSeriesView.Full,
        };

        // Distinct awake hour-buckets (UTC, floored to the hour). Using a set keeps the count
        // correct even if the API returns more than one series for the window.
        var awakeHours = new HashSet<DateTime>();
        await foreach (var series in client.ListTimeSeriesAsync(request).WithCancellation(cancellationToken))
        {
            foreach (var point in series.Points)
            {
                if (point.Value.Int64Value <= 0 && point.Value.DoubleValue <= 0)
                {
                    continue;
                }

                var bucket = point.Interval.StartTime.ToDateTime();
                awakeHours.Add(new DateTime(bucket.Year, bucket.Month, bucket.Day, bucket.Hour, 0, 0, DateTimeKind.Utc));
            }
        }

        var hoursByDay = awakeHours
            .GroupBy(h => DateOnly.FromDateTime(h))
            .ToDictionary(g => g.Key, g => g.Count());

        var days = new List<UptimeDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            days.Add(new UptimeDay(day, hoursByDay.GetValueOrDefault(day)));
        }

        return new UptimeCalendar(from, to, days);
    }

    /// <summary>
    /// State + next-run time of the Cloud Scheduler idle-check job, for the dashboard's
    /// "next check in X" countdown. Returns "Paused" once the fleet has slept (the
    /// controller pauses the job), which the UI uses to hide the countdown.
    /// </summary>
    private async Task<IdleCheckStatus> QueryIdleCheckAsync(CancellationToken cancellationToken)
    {
        var client = await _schedulerClient.Value;
        var name = JobName.FromProjectLocationJob(_options.ProjectId, _options.Region, _options.SchedulerJob);
        var job = await client.GetJobAsync(name, cancellationToken);

        DateTimeOffset? next = job.ScheduleTime is { } ts ? ts.ToDateTimeOffset() : null;
        return new IdleCheckStatus(job.State.ToString(), next);
    }

    /// <summary>
    /// The fleet is "asleep" when both MIGs have been scaled to 0 by the controller.
    /// Read-only — uses the same Compute credentials as the instance listing.
    /// </summary>
    private async Task<bool> QueryAsleepAsync(CancellationToken cancellationToken)
    {
        var client = await _migClient.Value;
        foreach (var mig in new[] { _options.ServicesMig, _options.GameServerMig })
        {
            var m = await client.GetAsync(_options.ProjectId, _options.Region, mig, cancellationToken);
            if (m.TargetSize != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Triggers the Cloud Run fleet controller's <c>/wake</c> endpoint. The dashboard
    /// holds no compute-write rights itself — the controller does the scaling. The
    /// controller has public ingress and gates /wake in-app by the shared admin key, so
    /// the dashboard authenticates with that header. Throws if the URL/key are unset.
    /// </summary>
    public async Task WakeFleetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FleetControllerUrl))
        {
            throw new InvalidOperationException("Gcp:FleetControllerUrl is not configured — run `terraform output fleet_controller_url` and set it.");
        }
        if (string.IsNullOrWhiteSpace(_options.FleetControllerAdminKey))
        {
            throw new InvalidOperationException("Gcp:FleetControllerAdminKey is not configured — run `terraform output -raw fleet_admin_key` and set it.");
        }

        var url = _options.FleetControllerUrl.TrimEnd('/');

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-ClashUp-Key", _options.FleetControllerAdminKey);
        var response = await http.PostAsync($"{url}/wake", content: null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Controller /wake returned {(int)response.StatusCode}: {body}");
        }

        _logger.LogInformation("Wake request sent to fleet controller at {Url}", url);
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
        // Both tiers report CCU under their own metric type; merge by instance id
        // (instances are unique across tiers, so one map serves both). Cloud
        // Monitoring forbids OR across metric.type, so query each type separately.
        var result = new Dictionary<string, List<VersionCcu>>(StringComparer.Ordinal);
        foreach (var metricType in new[] { GameServerCcuMetric, ServicesCcuMetric })
        {
            await foreach (var series in ListSeriesAsync($"metric.type = \"{metricType}\"", cancellationToken))
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

    /// <summary>
    /// Deletes a specific image version (by image name + tag) from Artifact
    /// Registry — the manual "retire this version" action behind the dashboard's
    /// delete button. Refuses anything tagged <c>latest</c> (it backs header-less
    /// internal routing and the active build), so only retired versions can go.
    /// Requires the running credentials to have delete rights
    /// (<c>roles/artifactregistry.repoAdmin</c>); the read-only dashboard SA does not
    /// by default.
    /// </summary>
    public async Task DeleteImageVersionAsync(string image, string tag, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(image) || string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("image and tag are required.");
        }

        if (string.Equals(tag, "latest", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete the 'latest' tag — it backs header-less internal routing.");
        }

        var client = await _registryClient.Value;
        var parent = $"projects/{_options.ProjectId}/locations/{_options.Region}/repositories/{_options.Repository}";

        string? digest = null;
        await foreach (var img in client.ListDockerImagesAsync(parent).WithCancellation(cancellationToken))
        {
            if (ParseImageName(img.Name) != image || !img.Tags.Contains(tag))
            {
                continue;
            }

            // The version a CI build publishes is double-tagged (e.g. "1.0.1" + "latest").
            // Deleting it by digest would take "latest" with it — refuse the active build.
            if (img.Tags.Any(t => string.Equals(t, "latest", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Refusing to delete '{image}:{tag}' — this version is also tagged 'latest' (the active build).");
            }

            var at = img.Name.IndexOf('@');
            digest = at >= 0 ? img.Name[(at + 1)..] : null;
            break;
        }

        if (string.IsNullOrEmpty(digest))
        {
            throw new InvalidOperationException($"No image '{image}' with tag '{tag}' found in the registry.");
        }

        var versionName = $"{parent}/packages/{image}/versions/{digest}";
        var op = await client.DeleteVersionAsync(versionName, cancellationToken);
        await op.PollUntilCompletedAsync();
        _logger.LogInformation("Deleted registry version {Image}:{Tag} ({Version})", image, tag, versionName);
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
