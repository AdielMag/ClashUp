using ClashUp.Server.Common.Gce;
using Google.Api;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClashUp.Server.Common.Ccu;

/// <summary>
/// Pushes an instance's CCU (from an <see cref="ICcuSource"/>) to Google Cloud
/// Monitoring as a custom gauge, labelled by server version. The GCP MIG
/// autoscaler consumes the gauge as a per-instance scaling signal and the local
/// dashboard reads it to show players-per-version.
///
/// The metric type is supplied per tier (e.g. <c>custom.googleapis.com/gameserver/ccu</c>
/// or <c>custom.googleapis.com/services/ccu</c>) so both tiers reuse this reporter
/// while reporting independent series.
///
/// No-ops when the GCE metadata server is unreachable (local/dev), so the host
/// runs identically off-cloud.
/// </summary>
public sealed class CcuMetricReporter : BackgroundService
{
    private readonly ICcuSource _source;
    private readonly string _metricType;
    private readonly int _intervalSeconds;
    private readonly ILogger<CcuMetricReporter> _logger;

    public CcuMetricReporter(
        ICcuSource source,
        string metricType,
        int intervalSeconds,
        ILogger<CcuMetricReporter> logger)
    {
        _source = source;
        _metricType = metricType;
        _intervalSeconds = intervalSeconds;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var instanceId = await GceMetadataHelper.GetInstanceIdAsync(stoppingToken).ConfigureAwait(false);
        var zone = await GceMetadataHelper.GetZoneAsync(stoppingToken).ConfigureAwait(false);
        var projectId = await GceMetadataHelper.GetProjectIdAsync(stoppingToken).ConfigureAwait(false);

        if (instanceId is null || zone is null || projectId is null)
        {
            _logger.LogInformation(
                "Not running on GCE (no metadata server) — CCU metric reporting disabled for {Metric}.", _metricType);
            return;
        }

        MetricServiceClient client;
        try
        {
            client = await new MetricServiceClientBuilder().BuildAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Cloud Monitoring client — CCU reporting disabled for {Metric}.", _metricType);
            return;
        }

        var projectName = new ProjectName(projectId);
        var version = ServerVersion.Current;
        var interval = TimeSpan.FromSeconds(Math.Max(5, _intervalSeconds));
        using var timer = new PeriodicTimer(interval);

        _logger.LogInformation(
            "CCU metric reporting enabled for {Metric} (instance {InstanceId}, zone {Zone}, version {Version}, every {Interval}s).",
            _metricType, instanceId, zone, version, interval.TotalSeconds);

        do
        {
            try
            {
                var series = BuildTimeSeries(instanceId, zone, projectId, version, _source.CurrentCcu);
                await client.CreateTimeSeriesAsync(projectName, new[] { series }, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push CCU metric {Metric}", _metricType);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private TimeSeries BuildTimeSeries(
        string instanceId, string zone, string projectId, string version, int ccu)
    {
        return new TimeSeries
        {
            Metric = new Metric
            {
                Type = _metricType,
                Labels = { { "version", version } },
            },
            Resource = new MonitoredResource
            {
                Type = "gce_instance",
                Labels =
                {
                    { "project_id", projectId },
                    { "instance_id", instanceId },
                    { "zone", zone },
                },
            },
            MetricKind = MetricDescriptor.Types.MetricKind.Gauge,
            ValueType = MetricDescriptor.Types.ValueType.Int64,
            Points =
            {
                new Point
                {
                    Interval = new TimeInterval { EndTime = Timestamp.FromDateTime(DateTime.UtcNow) },
                    Value = new TypedValue { Int64Value = ccu },
                },
            },
        };
    }
}
