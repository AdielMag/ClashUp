using ClashUp.Server.Common;
using ClashUp.Server.Common.Gce;
using ClashUp.Server.GameServer.Match;
using Google.Api;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.GameServer.Ccu;

/// <summary>
/// Pushes this instance's CCU to Google Cloud Monitoring as the custom gauge
/// <c>custom.googleapis.com/gameserver/ccu</c>, labelled by server version. The
/// GCP MIG autoscaler consumes this metric as a per-instance scaling signal, and
/// the local dashboard reads it to show players-per-version.
///
/// No-ops when the GCE metadata server is unreachable (local/dev), so the host
/// runs identically off-cloud.
/// </summary>
public sealed class CcuMetricReporter : BackgroundService
{
    private const string MetricType = "custom.googleapis.com/gameserver/ccu";

    private readonly ICcuTracker _ccuTracker;
    private readonly GameServerOptions _options;
    private readonly ILogger<CcuMetricReporter> _logger;

    public CcuMetricReporter(
        ICcuTracker ccuTracker,
        IOptions<GameServerOptions> options,
        ILogger<CcuMetricReporter> logger)
    {
        _ccuTracker = ccuTracker;
        _options = options.Value;
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
                "Not running on GCE (no metadata server) — CCU metric reporting disabled.");
            return;
        }

        MetricServiceClient client;
        try
        {
            client = await new MetricServiceClientBuilder().BuildAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Cloud Monitoring client — CCU reporting disabled.");
            return;
        }

        var projectName = new ProjectName(projectId);
        var version = ServerVersion.Current;
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.CcuReportIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        _logger.LogInformation(
            "CCU metric reporting enabled (instance {InstanceId}, zone {Zone}, version {Version}, every {Interval}s).",
            instanceId, zone, version, interval.TotalSeconds);

        do
        {
            try
            {
                var series = BuildTimeSeries(instanceId, zone, projectId, version, _ccuTracker.CurrentCcu);
                await client.CreateTimeSeriesAsync(projectName, new[] { series }, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push CCU metric");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private static TimeSeries BuildTimeSeries(
        string instanceId, string zone, string projectId, string version, int ccu)
    {
        return new TimeSeries
        {
            Metric = new Metric
            {
                Type = MetricType,
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
