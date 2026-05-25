using Novolis.Storage.Abstractions;

namespace Novolis.Storage.InMemory;

internal sealed class InMemoryRepositoryProvider(IInMemoryStore store) : IRepositoryProvider
{
    public IRepository<T> Create<T>() where T : class, IHasId => new InMemoryRepository<T>(store);
}
