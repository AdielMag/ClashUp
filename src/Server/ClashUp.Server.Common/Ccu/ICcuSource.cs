namespace ClashUp.Server.Common.Ccu;

/// <summary>
/// Exposes a tier's current Concurrent Connected Users (CCU) count so the shared
/// <see cref="CcuMetricReporter"/> can push it to Cloud Monitoring. Each tier
/// implements this over its own notion of "connected user": match players on the
/// GameServer tier, live hub connections on the Services tier.
/// </summary>
public interface ICcuSource
{
    /// <summary>Current number of counted users on this instance.</summary>
    int CurrentCcu { get; }
}
