using Novolis.Storage.Abstractions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Novolis.Storage.Sqlite;

/// <summary>
/// Registers SQLite storage for a keyed entity type.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="ISqliteClient"/>, connection options, and <see cref="SqliteRepository{T}"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Entity type stored in SQLite.</typeparam>
    /// <param name="services">Service collection to extend.</param>
    /// <param name="configuration">Configuration supplying an optional <c>SqliteConnection</c> connection string.</param>
    /// <param name="databaseName">Optional database file name when no connection string is configured.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSqliteDataStorage<T>(this IServiceCollection services, IConfiguration configuration, string? databaseName = null) where T : class, IKeyed, new()
    {
        var connectionString = configuration.GetConnectionString(nameof(SqliteConnection));
        databaseName ??= "Storage.db";
        connectionString ??= $"Data Source={Path.Combine(AppContext.BaseDirectory, "SqliteData", databaseName)}";
        services.AddSingletonIfNotRegistered<IOptions<SqliteConnection>>(Options.Create(new SqliteConnection { ConnectionString = connectionString }));
        services.AddSingletonIfNotRegistered<ISqliteClient, SqliteClient>();
        services.AddDataStorageRepository<SqliteRepository<T>, T>();
        return services;
    }
}
