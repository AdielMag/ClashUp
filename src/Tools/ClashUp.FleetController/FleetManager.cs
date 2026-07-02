using Google.Cloud.Compute.V1;
using Google.Cloud.Monitoring.V3;
using Google.Cloud.Scheduler.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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
    private readonly AtlasAccessListClient _atlas;
    private readonly ILogger<FleetManager> _logger;

    private readonly Lazy<Task<RegionInstanceGroupManagersClient>> _migClient;
    private readonly Lazy<Task<RegionAutoscalersClient>> _autoscalerClient;
    private readonly Lazy<Task<MetricServiceClient>> _metricClient;
    private readonly Lazy<Task<CloudSchedulerClient>> _schedulerClient;
    private readonly Lazy<Task<AddressesClient>> _addressClient;
    private readonly Lazy<Task<ForwardingRulesClient>> _forwardingRuleClient;
    private readonly Lazy<Task<RoutersClient>> _routerClient;
    private readonly Lazy<Task<RegionBackendServicesClient>> _backendServiceClient;

    // Cloud Run runs this service at max 1 instance, so a single in-process lock
    // fully serializes concurrent /resolve + /wake + /tick calls — only one wake
    // mutates networking at a time; the rest observe the finished state.
    private readonly SemaphoreSlim _networkLock = new(1, 1);

    public FleetManager(
        IOptions<FleetControllerOptions> options,
        AtlasAccessListClient atlas,
        ILogger<FleetManager> logger)
    {
        _o = options.Value;
        _atlas = atlas;
        _logger = logger;

        // Credentials come from Application Default Credentials (the Cloud Run service SA).
        _migClient = new(() => new RegionInstanceGroupManagersClientBuilder().BuildAsync());
        _autoscalerClient = new(() => new RegionAutoscalersClientBuilder().BuildAsync());
        _metricClient = new(() => new MetricServiceClientBuilder().BuildAsync());
        _schedulerClient = new(() => new CloudSchedulerClientBuilder().BuildAsync());
        _addressClient = new(() => new AddressesClientBuilder().BuildAsync());
        _forwardingRuleClient = new(() => new ForwardingRulesClientBuilder().BuildAsync());
        _routerClient = new(() => new RoutersClientBuilder().BuildAsync());
        _backendServiceClient = new(() => new RegionBackendServicesClientBuilder().BuildAsync());
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

    /// <summary>Wake both tiers, rebuild networking, re-arm the idle check. Idempotent.</summary>
    public async Task<ActionResult> WakeAsync(CancellationToken ct)
    {
        await _networkLock.WaitAsync(ct);
        try
        {
            // Turning the autoscaler back ON makes it restore min_replicas (>=1) on its own.
            // We must NOT manually resize an autoscaled (mode=ON) MIG — the Compute API rejects
            // that ("Resizing of autoscaled regional managed instance groups is not allowed").
            // So wake = flip mode ON; the autoscaler scales the tier back up itself.
            foreach (var tier in Tiers)
            {
                await SetAutoscalerModeAsync(tier.Autoscaler, "ON", ct);
            }

            // Re-provision the torn-down networking (fresh IPs, forwarding rule, NAT, Atlas).
            var servicesIp = await EnsureNetworkingUpAsync(ct);

            await ResumeScheduleAsync(ct);
            return new ActionResult(true,
                $"Fleet waking — autoscalers ON, networking up (Services {servicesIp}), idle check re-armed.");
        }
        finally
        {
            _networkLock.Release();
        }
    }

    /// <summary>
    /// Boot-time discovery for the client: return the live Services endpoint, waking the
    /// fleet (and provisioning networking) if it is currently asleep. Public + bounded-cost:
    /// the worst a spammer can do is keep the fleet awake, which the idle check reaps anyway.
    /// </summary>
    public async Task<string> ResolveServicesEndpointAsync(CancellationToken ct)
    {
        var existing = await TryGetAddressAsync(_o.ServicesIpName, ct);
        if (existing is not null && await ForwardingRuleExistsAsync(ct))
        {
            // Fast path — already awake, don't touch anything.
            return FormatEndpoint(existing.Address_);
        }

        await WakeAsync(ct);
        var ip = (await TryGetAddressAsync(_o.ServicesIpName, ct))?.Address_
            ?? throw new InvalidOperationException("Wake completed but Services IP is missing.");
        return FormatEndpoint(ip);
    }

    private string FormatEndpoint(string ip) => $"http://{ip}:{_o.ServicesPort}";

    private async Task SleepAsync(CancellationToken ct)
    {
        await _networkLock.WaitAsync(ct);
        try
        {
            foreach (var tier in Tiers)
            {
                // Disable the autoscaler FIRST (and wait for it to apply) — you can't resize an
                // autoscaled MIG, and an ON autoscaler would re-create the minimum instantly.
                await SetAutoscalerModeAsync(tier.Autoscaler, "OFF", ct);
                await ResizeAsync(tier.Mig, 0, ct);
            }

            // Instances are gone — release everything that bills while idle.
            await TearDownNetworkingAsync(ct);
        }
        finally
        {
            _networkLock.Release();
        }
    }

    // --- Networking teardown / recreate -------------------------------------

    /// <summary>
    /// Allocate the Services + NAT IPs, bind the forwarding rule to the (persistent)
    /// backend service, recreate the Cloud NAT, and re-allowlist the NAT IP in Atlas.
    /// Every step is get-then-act idempotent so concurrent/duplicate wakes are safe.
    /// </summary>
    private async Task<string> EnsureNetworkingUpAsync(CancellationToken ct)
    {
        var servicesIp = await EnsureAddressAsync(_o.ServicesIpName, ct);
        await EnsureForwardingRuleAsync(servicesIp.Address_, ct);

        var natIp = await EnsureAddressAsync(_o.NatIpName, ct);
        await SetNatAsync(enabled: true, natIp.SelfLink, ct);
        await _atlas.EnsureOnlyAsync(natIp.Address_, ct);

        return servicesIp.Address_;
    }

    private async Task TearDownNetworkingAsync(CancellationToken ct)
    {
        // Order matters: the forwarding rule and NAT reference the IPs, so drop the
        // dependents before releasing the addresses.
        await DeleteForwardingRuleAsync(ct);
        await ReleaseAddressAsync(_o.ServicesIpName, ct);

        await SetNatAsync(enabled: false, natIpSelfLink: null, ct);
        await ReleaseAddressAsync(_o.NatIpName, ct);
        // Atlas entry is left in place; the next wake prunes stale entries when it
        // adds the fresh NAT IP. Nothing bills for an allowlist entry.
    }

    private async Task<Address> EnsureAddressAsync(string name, CancellationToken ct)
    {
        var existing = await TryGetAddressAsync(name, ct);
        if (existing is not null)
        {
            return existing;
        }

        var client = await _addressClient.Value;
        var op = await client.InsertAsync(_o.ProjectId, _o.Region, new Address
        {
            Name = name,
            AddressType = "EXTERNAL",
            NetworkTier = "PREMIUM",
        }, ct);
        await op.PollUntilCompletedAsync();
        _logger.LogInformation("Allocated address {Name}", name);

        return await client.GetAsync(_o.ProjectId, _o.Region, name, ct);
    }

    private async Task<Address?> TryGetAddressAsync(string name, CancellationToken ct)
    {
        var client = await _addressClient.Value;
        try
        {
            return await client.GetAsync(_o.ProjectId, _o.Region, name, ct);
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task ReleaseAddressAsync(string name, CancellationToken ct)
    {
        if (await TryGetAddressAsync(name, ct) is null)
        {
            return;
        }

        var client = await _addressClient.Value;
        var op = await client.DeleteAsync(_o.ProjectId, _o.Region, name, ct);
        await op.PollUntilCompletedAsync();
        _logger.LogInformation("Released address {Name}", name);
    }

    private async Task<bool> ForwardingRuleExistsAsync(CancellationToken ct)
    {
        var client = await _forwardingRuleClient.Value;
        try
        {
            await client.GetAsync(_o.ProjectId, _o.Region, _o.ServicesForwardingRule, ct);
            return true;
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            return false;
        }
    }

    private async Task EnsureForwardingRuleAsync(string servicesIp, CancellationToken ct)
    {
        if (await ForwardingRuleExistsAsync(ct))
        {
            return;
        }

        var backends = await _backendServiceClient.Value;
        var backend = await backends.GetAsync(_o.ProjectId, _o.Region, _o.ServicesBackendService, ct);

        var client = await _forwardingRuleClient.Value;
        var op = await client.InsertAsync(_o.ProjectId, _o.Region, new ForwardingRule
        {
            Name = _o.ServicesForwardingRule,
            LoadBalancingScheme = "EXTERNAL",
            IPProtocol = "TCP",
            Ports = { _o.ServicesPort.ToString() },
            IPAddress = servicesIp,
            BackendService = backend.SelfLink,
        }, ct);
        await op.PollUntilCompletedAsync();
        _logger.LogInformation("Created forwarding rule {Name} -> {Ip}", _o.ServicesForwardingRule, servicesIp);
    }

    private async Task DeleteForwardingRuleAsync(CancellationToken ct)
    {
        if (!await ForwardingRuleExistsAsync(ct))
        {
            return;
        }

        var client = await _forwardingRuleClient.Value;
        var op = await client.DeleteAsync(_o.ProjectId, _o.Region, _o.ServicesForwardingRule, ct);
        await op.PollUntilCompletedAsync();
        _logger.LogInformation("Deleted forwarding rule {Name}", _o.ServicesForwardingRule);
    }

    // Cloud NAT is a nested config on the Router. We GET the router, add/remove our
    // NAT entry, then full-PUT (Update) so immutable fields (network) round-trip
    // unchanged — same pattern the autoscaler mode flip uses.
    private async Task SetNatAsync(bool enabled, string? natIpSelfLink, CancellationToken ct)
    {
        var client = await _routerClient.Value;
        var router = await client.GetAsync(_o.ProjectId, _o.Region, _o.Router, ct);

        var already = router.Nats.Any(n => n.Name == _o.NatName);
        if (enabled == already)
        {
            _logger.LogInformation("Cloud NAT {Name} already {State}", _o.NatName, enabled ? "present" : "absent");
            return;
        }

        if (enabled)
        {
            router.Nats.Add(new RouterNat
            {
                Name = _o.NatName,
                NatIpAllocateOption = "MANUAL_ONLY",
                NatIps = { natIpSelfLink },
                SourceSubnetworkIpRangesToNat = "ALL_SUBNETWORKS_ALL_IP_RANGES",
            });
        }
        else
        {
            var toRemove = router.Nats.Where(n => n.Name == _o.NatName).ToList();
            foreach (var nat in toRemove)
            {
                router.Nats.Remove(nat);
            }
        }

        var op = await client.UpdateAsync(_o.ProjectId, _o.Region, _o.Router, router, ct);
        await op.PollUntilCompletedAsync();
        _logger.LogInformation("Cloud NAT {Name} -> {State}", _o.NatName, enabled ? "created" : "removed");
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
    // The update is awaited to completion so a following resize sees the new mode.
    private async Task SetAutoscalerModeAsync(string autoscaler, string mode, CancellationToken ct)
    {
        var client = await _autoscalerClient.Value;
        var current = await client.GetAsync(_o.ProjectId, _o.Region, autoscaler, ct);
        if (string.Equals(current.AutoscalingPolicy.Mode, mode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Autoscaler {Autoscaler} already {Mode}", autoscaler, mode);
            return;
        }

        current.AutoscalingPolicy.Mode = mode;
        var op = await client.UpdateAsync(_o.ProjectId, _o.Region, current, ct);
        await op.PollUntilCompletedAsync();
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
