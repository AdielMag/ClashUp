using System.Threading;
using System.Threading.Tasks;

namespace ClashUp.Server.Common.Mongo;

/// <summary>
/// Implementations declare the indexes their collections require and
/// apply them idempotently at startup. Adding a new query path without
/// a matching index implementation is a review blocker — see
/// docs/rules/mongo-data.md.
/// </summary>
public interface IIndexInitializer
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken);
}
