using MongoDB.Bson.Serialization.Attributes;

namespace ClashUp.Server.Services.Persistence;

public sealed class GameServerInstanceDoc
{
    [BsonId]
    public string InstanceId { get; set; } = string.Empty;

    public string PublicEndpoint { get; set; } = string.Empty;

    public string InternalEndpoint { get; set; } = string.Empty;

    public int CapacityMax { get; set; }

    public int CapacityUsed { get; set; }

    /// <summary>Concurrent connected users on this instance (with disconnect grace applied).</summary>
    public int Ccu { get; set; }

    public string Version { get; set; } = string.Empty;

    public DateTime LastHeartbeatAt { get; set; }

    /// <summary>Healthy | Draining | Unhealthy.</summary>
    public string Status { get; set; } = "Healthy";
}
