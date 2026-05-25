using System.Collections.Concurrent;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.InMemory;

/// <summary>
/// Thread-safe in-memory store: one <see cref="ConcurrentDictionary{Guid,T}"/> per entity type.
/// </summary>
internal sealed class InMemoryStore : IInMemoryStore
{
    private readonly ConcurrentDictionary<Type, object> _stores = new();

    public ConcurrentDictionary<Guid, T> GetOrAddDictionary<T>() where T : class, IHasId
    {
        return (ConcurrentDictionary<Guid, T>)_stores.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<Guid, T>());
    }
}
