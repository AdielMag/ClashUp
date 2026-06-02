using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class GsRegistration
    {
        [Key(0)] public string InstanceId { get; init; } = string.Empty;
        [Key(1)] public string PublicEndpoint { get; init; } = string.Empty;
        [Key(2)] public int CapacityMax { get; init; }
        [Key(3)] public string Version { get; init; } = string.Empty;
        [Key(4)] public string InternalEndpoint { get; init; } = string.Empty;
    }

    [MessagePackObject]
    public sealed class GsToken
    {
        [Key(0)] public string InstanceId { get; init; } = string.Empty;
        [Key(1)] public string BearerJwt { get; init; } = string.Empty;
    }

    [MessagePackObject]
    public sealed class GsHeartbeat
    {
        [Key(0)] public string InstanceId { get; init; } = string.Empty;
        [Key(1)] public int CapacityUsed { get; init; }
        [Key(2)] public double CpuLoad { get; init; }
    }

    [MessagePackObject]
    public sealed class GsMatchStarted
    {
        [Key(0)] public string InstanceId { get; init; } = string.Empty;
        [Key(1)] public MatchId MatchId { get; init; }
    }

    [MessagePackObject]
    public sealed class GsMatchEnded
    {
        [Key(0)] public string InstanceId { get; init; } = string.Empty;
        [Key(1)] public MatchId MatchId { get; init; }
        [Key(2)] public MatchResult? Result { get; init; }
    }
}
