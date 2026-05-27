using MongoDB.Bson.Serialization.Attributes;

namespace ClashUp.Server.Services.Persistence;

public sealed class MatchDoc
{
    [BsonId]
    public string MatchId { get; set; } = string.Empty;

    public string GsInstanceId { get; set; } = string.Empty;

    public string GsEndpoint { get; set; } = string.Empty;

    /// <summary>Provisioning | Active | Ended.</summary>
    public string State { get; set; } = "Provisioning";

    public List<MatchPlayerDoc> Players { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class MatchPlayerDoc
{
    public string PlayerId { get; set; } = string.Empty;
    public int TeamId { get; set; }
}
