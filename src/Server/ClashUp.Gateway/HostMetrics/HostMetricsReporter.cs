using ClashUp.Server.Common.Gce;
using Google.Api;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.Gateway.HostMetrics;

/// <summary>
/// Pushes host memory utilization to Cloud Monitoring as
/// <c>custom.googleapis.com/instance/memory_utilization</c>, labelled by
/// instance. This replaces the Ops Agent's RAM metric so the instances can run
/// on Container-Optimized OS — the gateway runs on every VM (both tiers), so one
/// reporter covers the whole fleet. CPU autoscaling stays native (no agent).
///
/// No-ops off-GCE (local/dev) where there is no metadata server.
/// </summary>
public sealed class HostMetricsReporter : BackgroundService
{
    private const string MetricType = "custom.googleapis.com/instance/memory_utilization";

    private readonly GatewayOptions _options;
    private readonly ILogger<HostMetricsReporter> _logger;

    public HostMetricsReporter(IOptions<GatewayOptions> options, ILogger<HostMetricsReporter> logger)
    {
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
            _logger.LogInformation("Not running on GCE — host memory metric reporting disabled.");
            return;
        }

        MetricServiceClient client;
        try
        {
            client = await new MetricServiceClientBuilder().BuildAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Cloud Monitoring client — memory reporting disabled.");
            return;
        }

        var projectName = new ProjectName(projectId);
        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.MetricsIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        _logger.LogInformation(
            "Host memory metric reporting enabled (instance {InstanceId}, every {Interval}s).",
            instanceId, interval.TotalSeconds);

        do
        {
            var percent = HostMemoryReader.ReadUtilizationPercent();
            if (percent is null)
            {
                continue;
            }

            try
            {
                var series = BuildTimeSeries(instanceId, zone, projectId, percent.Value);
                await client.CreateTimeSeriesAsync(projectName, new[] { series }, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push host memory metric");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private static TimeSeries BuildTimeSeries(string instanceId, string zone, string projectId, double percent)
    {
        return new TimeSeries
        {
            Metric = new Metric { Type = MetricType },
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
            ValueType = MetricDescriptor.Types.ValueType.Double,
            Points =
            {
                new Point
                {
                    Interval = new TimeInterval { EndTime = Timestamp.FromDateTime(DateTime.UtcNow) },
                    Value = new TypedValue { DoubleValue = percent },
                },
            },
        };
    }
}
