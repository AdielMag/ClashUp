using MongoDB.Driver;

namespace ClashUp.Server.Common.Mongo;

/// <summary>
/// Single entry point to the configured Mongo database. Repository
/// classes depend on this; nothing else should. See docs/rules/mongo-data.md.
/// </summary>
public interface IMongoContext
{
    IMongoDatabase Database { get; }

    IMongoCollection<TDocument> GetCollection<TDocument>(string name);
}
