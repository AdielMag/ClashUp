using Google.Cloud.Compute.V1;
using Google.Cloud.Monitoring.V3;
using Google.Cloud.Scheduler.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;

namespace ClashUp.Tools.FleetController;

/// <summary>
/// Drives the cost-saving sleep/wake loop over the two regional MIGs.
///
/// GCP MIG autoscalers cannot scale below 1, so "sleep" means: turn the autoscaler
/// OFF (otherwise it immediately re-creates the minimum) and resize the MIG to 0.
/// "Wake" turns it back ON and resizes to <see cref="FleetControllerOptions.WakeSize"/>.
/// Idle is judged from the per-instance CCU gauges both tiers push to Cloud Monitoring.
/// </summary>
public sealed class FleetManager
{
    private const string GameServerCcuMetric = "custom.googleapis.com/gameserver/ccu";
    private const string ServicesCcuMetric = "custom.googleapis.com/services/ccu";

    private readonly FleetControllerOptions _o;
    private readonly ILogger<FleetManager> _logger;

    private readonly Lazy<Task<RegionInstanceGroupManagersClient>> _migClient;
    private readonly Lazy<Task<RegionAutoscalersClient>> _autoscalerClient;
    private readonly Lazy<Task<MetricServiceClient>> _metricClient;
    private readonly Lazy<Task<CloudSchedulerClient>> _schedulerClient;

    public FleetManager(IOptions<FleetControllerOptions> options, ILogger<FleetManager> logger)
    {
        _o = options.Value;
        _logger = logger;

        // Credentials come from Application Default Credentials (the Cloud Run service SA).
        _migClient = new(() => new RegionInstanceGroupManagersClientBuilder().BuildAsync());
        _autoscalerClient = new(() => new RegionAutoscalersClientBuilder().BuildAsync());
        _metricClient = new(() => new MetricServiceClientBuilder().BuildAsync());
        _schedulerClient = new(() => new CloudSchedulerClientBuilder().BuildAsync());
    }

    private IReadOnlyList<Tier> Tiers => new[]
    {
        new Tier("Services", _o.ServicesMig, _o.ServicesAutoscaler),
        new Tier("GameServer", _o.GameServerMig, _o.GameServerAutoscaler),
    };

    /// <summary>Current target size per tier + the derived asleep flag (both at 0).</summary>
    public async Task<FleetState> GetStateAsync(CancellationToken ct)
    {
        var client = await _migClient.Value;
        var tiers = new List<TierState>();
        foreach (var tier in Tiers)
        {
            var mig = await client.GetAsync(_o.ProjectId, _o.Region, tier.Mig, ct);
            tiers.Add(new TierState(tier.Name, mig.TargetSize));
        }

        return new FleetState(tiers.All(t => t.TargetSize == 0), tiers);
    }

    /// <summary>Scheduler-driven idle check: sleep + self-pause when no one is online.</summary>
    public async Task<ActionResult> TickAsync(CancellationToken ct)
    {
        var state = await GetStateAsync(ct);
        if (state.Asleep)
        {
            return new ActionResult(false, "Already asleep — nothing to do.");
        }

        if (!await IsIdleAsync(ct))
        {
            return new ActionResult(false, "Players online — staying awake.");
        }

        await SleepAsync(ct);
        await PauseScheduleAsync(ct);
        return new ActionResult(true, "Fleet idle — scaled both tiers to 0 and paused the idle check.");
    }

    /// <summary>Wake both tiers and re-arm the idle check. Idempotent.</summary>
    public async Task<ActionResult> WakeAsync(CancellationToken ct)
    {
        foreach (var tier in Tiers)
        {
            await SetAutoscalerModeAsync(tier.Autoscaler, "ON", ct);
            await ResizeAsync(tier.Mig, _o.WakeSize, ct);
        }

        await ResumeScheduleAsync(ct);
        return new ActionResult(true, "Fleet waking — scaling both tiers up and re-armed the idle check.");
    }

    private async Task SleepAsync(CancellationToken ct)
    {
        foreach (var tier in Tiers)
        {
            // Disable the autoscaler FIRST — otherwise it re-creates the minimum the
            // instant we resize to 0.
            await SetAutoscalerModeAsync(tier.Autoscaler, "OFF", ct);
            await ResizeAsync(tier.Mig, 0, ct);
        }
    }

    private async Task<bool> IsIdleAsync(CancellationToken ct)
    {
        var client = await _metricClient.Value;
        foreach (var metric in new[] { GameServerCcuMetric, ServicesCcuMetric })
        {
            var request = new ListTimeSeriesRequest
            {
                ProjectName = new Google.Api.Gax.ResourceNames.ProjectName(_o.ProjectId),
                Filter = $"metric.type = \"{metric}\"",
                Interval = new TimeInterval
                {
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-_o.IdleLookbackMinutes)),
                    EndTime = Timestamp.FromDateTime(DateTime.UtcNow),
                },
                View = ListTimeSeriesRequest.Types.TimeSeriesView.Full,
            };

            await foreach (var series in client.ListTimeSeriesAsync(request).WithCancellation(ct))
            {
                // Any non-zero point anywhere in the window means someone was connected — not idle.
                if (series.Points.Any(p => p.Value.Int64Value > 0))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private async Task ResizeAsync(string mig, int size, CancellationToken ct)
    {
        var client = await _migClient.Value;
        await client.ResizeAsync(_o.ProjectId, _o.Region, mig, size, ct);
        _logger.LogInformation("Resized {Mig} -> {Size}", mig, size);
    }

    // mode is the Compute API string value: "ON" or "OFF". We GET the full autoscaler,
    // flip just the mode, then UPDATE (full replace) so the rest of the policy is preserved.
    private async Task SetAutoscalerModeAsync(string autoscaler, string mode, CancellationToken ct)
    {
        var client = await _autoscalerClient.Value;
        var current = await client.GetAsync(_o.ProjectId, _o.Region, autoscaler, ct);
        current.AutoscalingPolicy.Mode = mode;
        await client.UpdateAsync(_o.ProjectId, _o.Region, current, ct);
        _logger.LogInformation("Autoscaler {Autoscaler} mode -> {Mode}", autoscaler, mode);
    }

    private async Task PauseScheduleAsync(CancellationToken ct)
    {
        var client = await _schedulerClient.Value;
        await client.PauseJobAsync(JobName.FromProjectLocationJob(_o.ProjectId, _o.Region, _o.SchedulerJob), ct);
    }

    private async Task ResumeScheduleAsync(CancellationToken ct)
    {
        var client = await _schedulerClient.Value;
        await client.ResumeJobAsync(JobName.FromProjectLocationJob(_o.ProjectId, _o.Region, _o.SchedulerJob), ct);
    }

    private readonly record struct Tier(string Name, string Mig, string Autoscaler);
}

public sealed record FleetState(bool Asleep, IReadOnlyList<TierState> Tiers);

public sealed record TierState(string Tier, int TargetSize);

public sealed record ActionResult(bool Changed, string Message);
