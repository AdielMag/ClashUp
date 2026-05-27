using MongoDB.Bson.Serialization.Attributes;

namespace ClashUp.Server.Services.Persistence;

public sealed class AccountDoc
{
    [BsonId]
    public string PlayerId { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
