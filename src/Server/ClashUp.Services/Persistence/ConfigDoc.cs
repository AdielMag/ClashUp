using MongoDB.Bson.Serialization.Attributes;

namespace ClashUp.Server.Services.Persistence;

public sealed class ConfigDoc
{
    [BsonId]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
