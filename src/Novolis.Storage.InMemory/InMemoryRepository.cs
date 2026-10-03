using System.Collections.Concurrent;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Query;

namespace Novolis.Storage.InMemory;

/// <summary>
/// Thread-safe in-memory <see cref="IRepository{T}"/> backed by <see cref="ConcurrentDictionary{Guid,T}"/>.
/// </summary>
internal sealed class InMemoryRepository<T>(IInMemoryStore store) :
    IRepository<T>,
    IRepositoryQueryProvider<T>
    where T : class, IHasId
{
    private readonly ConcurrentDictionary<Guid, T> _dict = store.GetOrAddDictionary<T>();

    public IEnumerable<T> All() => _dict.Values;

    public ValueTask<T?> TryGetAsync(Guid id, CancellationToken ct = default)
    {
        _dict.TryGetValue(id, out var value);
        return ValueTask.FromResult(value);
    }

    public ValueTask UpsertAsync(T entity, CancellationToken ct = default)
    {
        _dict[entity.Id] = entity;
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return ValueTask.FromResult(_dict.TryRemove(id, out _));
    }

    public ValueTask<QueryPage<T>> ExecuteAsync(Query<T> query, CancellationToken cancellationToken = default) =>
        InMemoryQueryExecutor.ExecuteAsync(_dict.Values, query, cancellationToken);

    public ValueTask<long> CountAsync(Query<T> query, CancellationToken cancellationToken = default) =>
        InMemoryQueryExecutor.CountAsync(_dict.Values, query, cancellationToken);
}
