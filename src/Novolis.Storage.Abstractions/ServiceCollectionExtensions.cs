using Microsoft.Extensions.DependencyInjection;

namespace Novolis.Storage.Abstractions;

/// <summary>
/// DI helpers for Novolis storage repositories.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="IRepository{T}"/> implementation.
    /// </summary>
    /// <typeparam name="TRepo">Repository implementation type.</typeparam>
    /// <typeparam name="T">Entity type stored by the repository.</typeparam>
    /// <param name="services">Service collection to extend.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a repository for <typeparamref name="T"/> is already registered.</exception>
    public static IServiceCollection AddDataStorageRepository<TRepo, T>(this IServiceCollection services) where TRepo : class, IRepository<T> where T : class, IKeyed, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(x => x.ServiceType == typeof(IRepository<T>)))
            throw new InvalidOperationException($"Repository for {typeof(T).Name} already registered.");
        services.AddSingleton<IRepository<T>, TRepo>();
        return services;
    }

    /// <summary>
    /// Adds a singleton service mapping when no existing registration is present.
    /// </summary>
    public static IServiceCollection AddSingletonIfNotRegistered<TService, TImplementation>(this IServiceCollection services) where TService : class where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(x => x.ServiceType == typeof(TService)))
            return services;
        services.AddSingleton<TService, TImplementation>();
        return services;
    }

    /// <summary>
    /// Adds a singleton instance when no existing registration is present.
    /// </summary>
    public static IServiceCollection AddSingletonIfNotRegistered<TService>(this IServiceCollection services, TService implementationInstance) where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(x => x.ServiceType == typeof(TService)))
            return services;
        services.AddSingleton(implementationInstance);
        return services;
    }

    /// <summary>
    /// Adds a singleton factory when no existing registration is present.
    /// </summary>
    public static IServiceCollection AddSingletonIfNotRegistered<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory) where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(x => x.ServiceType == typeof(TService)))
            return services;
        services.AddSingleton(implementationFactory);
        return services;
    }

    /// <summary>
    /// Adds a singleton with default constructor when no existing registration is present.
    /// </summary>
    public static IServiceCollection AddSingletonIfNotRegistered<TService>(this IServiceCollection services) where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(x => x.ServiceType == typeof(TService)))
            return services;
        services.AddSingleton<TService>();
        return services;
    }
}
