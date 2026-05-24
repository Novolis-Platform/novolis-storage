using Novolis.Storage.Abstractions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Novolis.Storage.Json;

/// <summary>
/// Registers JSON file storage for a keyed entity type.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="JsonContext"/>, connection options, and <see cref="JsonRepository{T}"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Entity type stored as JSON files.</typeparam>
    /// <param name="services">Service collection to extend.</param>
    /// <param name="configuration">Configuration supplying an optional <c>JsonConnection</c> connection string.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddJsonDataStorage<T>(this IServiceCollection services, IConfiguration configuration) where T : class, IKeyed, new()
    {
        var connectionString = configuration.GetConnectionString(nameof(JsonConnection));
        connectionString ??= Path.Combine(AppContext.BaseDirectory, "JsonData");
        services.AddSingletonIfNotRegistered<IOptions<JsonConnection>>(Options.Create(new JsonConnection { JsonDataFolder = connectionString }));
        services.AddSingletonIfNotRegistered<JsonContext>();
        services.AddDataStorageRepository<JsonRepository<T>, T>();
        return services;
    }
}
