using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Query;

/// <summary>Executes a validated finite query for a repository provider.</summary>
public interface IRepositoryQueryProvider<T> where T : class, IHasId
{
    /// <summary>Executes the query and returns one finite page.</summary>
    ValueTask<QueryPage<T>> ExecuteAsync(Query<T> query, CancellationToken cancellationToken = default);

    /// <summary>Counts entities matched by the query predicates.</summary>
    ValueTask<long> CountAsync(Query<T> query, CancellationToken cancellationToken = default);
}
