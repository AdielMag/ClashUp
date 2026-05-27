using ClashUp.Server.Common.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace ClashUp.Server.Common.Mongo;

public sealed class MongoContext : IMongoContext
{
    public MongoContext(IOptions<MongoOptions> options)
    {
        var opts = options.Value;
        var client = new MongoClient(opts.ConnectionString);
        Database = client.GetDatabase(opts.DatabaseName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<TDocument> GetCollection<TDocument>(string name) =>
        Database.GetCollection<TDocument>(name);
}
