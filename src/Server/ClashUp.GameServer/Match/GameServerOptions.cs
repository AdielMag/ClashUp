namespace ClashUp.Server.GameServer.Match;

public sealed class GameServerOptions
{
    public const string SectionName = "GameServer";

    /// <summary>Identifier registered with the Services tier. Generated at first boot if blank.</summary>
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>Public address clients reach this host on (used in MatchHandoff).</summary>
    public string PublicEndpoint { get; init; } = "http://localhost:5101";

    /// <summary>Address the Services tier uses to call this GS (e.g. Docker service name). Defaults to PublicEndpoint if empty.</summary>
    public string InternalEndpoint { get; init; } = string.Empty;

    public int MaxConcurrentMatches { get; init; } = 8;

    public int DefaultTickRateHz { get; init; } = 30;

    /// <summary>Where the Services tier's gRPC endpoint lives.</summary>
    public string ServicesEndpoint { get; init; } = "http://localhost:5001";

    public int HeartbeatIntervalSeconds { get; init; } = 2;

    /// <summary>
    /// How long a disconnected player keeps counting toward CCU before being
    /// removed (smooths brief mobile disconnects). Reconnecting within this
    /// window cancels the pending removal. Default 45s — long enough to ride out
    /// a transient mobile reconnect without thrashing the autoscaler, short enough
    /// that leaving a match drops the count promptly.
    /// </summary>
    public int CcuGracePeriodSeconds { get; init; } = 45;

    /// <summary>How often the CCU metric is pushed to Cloud Monitoring, in seconds.</summary>
    public int CcuReportIntervalSeconds { get; init; } = 30;
}
