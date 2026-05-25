using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Novolis.Storage.Abstractions;

/// <summary>
/// Extension methods for adding storage services to the dependency injection container.
/// </summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Adds storage services and runs the given configuration on the builder.
    /// Register default provider(s) first, then use <see cref="StorageTypeBindingExtensions.BindRepository{T,TProvider}"/> for overrides.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration that registers providers and optional type bindings.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddStorage(
        this IServiceCollection services,
        Action<IStorageBuilder> configure)
    {
        services.TryAddSingleton<IIdProvider, GuidV7IdProvider>();
        services.TryAddSingleton<IRepositoryFactory, RepositoryFactory>();

        configure(new StorageBuilder(services));
        return services;
    }

    private sealed class StorageBuilder(IServiceCollection services) : IStorageBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
