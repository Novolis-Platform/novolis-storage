using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.InMemory;

/// <summary>
/// Registers in-memory storage as the default repository backend using thread-safe <see cref="ConcurrentDictionary{Guid,T}"/> per entity type.
/// </summary>
public static class InMemoryStorageExtensions
{
    /// <summary>
    /// Adds the in-memory provider: <see cref="IInMemoryStore"/> singleton, <see cref="IRepositoryProvider"/>, and open generic <see cref="IRepository{T}"/>.
    /// </summary>
    public static IStorageBuilder AddInMemoryProvider(this IStorageBuilder builder)
    {
        builder.Services.AddSingleton<IInMemoryStore, InMemoryStore>();
        builder.Services.AddSingleton<IRepositoryProvider, InMemoryRepositoryProvider>();
        builder.Services.AddTransient(typeof(IRepository<>), typeof(InMemoryRepository<>));
        return builder;
    }
}
