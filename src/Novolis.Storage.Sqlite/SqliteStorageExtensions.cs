using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Sqlite;

/// <summary>
/// Registers SQLite as the default repository backend and exposes <see cref="ISqliteClient"/> for legacy or custom usage.
/// </summary>
public static class SqliteStorageExtensions
{
    /// <summary>
    /// Adds the SQLite provider: options, <see cref="ISqliteClient"/> singleton, <see cref="IRepositoryProvider"/>, and open generic <see cref="IRepository{T}"/>.
    /// </summary>
    public static IStorageBuilder AddSqliteProvider(
        this IStorageBuilder builder,
        Action<SqliteOptions> configure)
    {
        var options = new SqliteOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            options.ConnectionString = "Data Source=:memory:";

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ISqliteClient, SqliteClient>();
        builder.Services.AddSingleton<IRepositoryProvider, SqliteRepositoryProvider>();
        builder.Services.AddTransient(typeof(IRepository<>), typeof(SqliteRepository<>));

        return builder;
    }
}
