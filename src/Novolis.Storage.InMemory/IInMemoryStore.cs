using System.Collections.Concurrent;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.InMemory;

/// <summary>
/// Thread-safe in-memory store that provides a <see cref="ConcurrentDictionary{Guid,T}"/> per entity type.
/// </summary>
public interface IInMemoryStore
{
    /// <summary>
    /// Gets or creates the dictionary for entity type <typeparamref name="T"/>.
    /// </summary>
    ConcurrentDictionary<Guid, T> GetOrAddDictionary<T>() where T : class, IHasId;
}
