using System.IO;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.LiteDb;

/// <summary>
/// Registers LiteDB as the default repository backend and exposes <see cref="ILiteDatabase"/> for legacy or custom usage.
/// </summary>
public static class LiteDbStorageExtensions
{
    /// <summary>
    /// Adds the LiteDB provider: options, <see cref="ILiteDatabase"/> singleton, <see cref="IRepositoryProvider"/>, and open generic <see cref="IRepository{T}"/>.
    /// The database file is opened on first resolution of <see cref="ILiteDatabase"/> (not at registration time) so hosts can run
    /// lightweight startup such as the desktop launcher readiness pipe before acquiring an exclusive file lock.
    /// </summary>
    public static IStorageBuilder AddLiteDbProvider(
        this IStorageBuilder builder,
        Action<LiteDbOptions> configure)
    {
        var opt = new LiteDbOptions();
        configure(opt);

        var connectionString = BuildConnectionString(opt);

        builder.Services.AddSingleton(opt);
        builder.Services.AddSingleton<ILiteDatabase>(_ =>
        {
            try
            {
                return new LiteDatabase(connectionString);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "Could not open the local LiteDB database file. Another game or tool instance may already be using it " +
                    "(close other Star Conflicts Revolt single-player clients that share this profile, then try again).",
                    ex);
            }
        });
        builder.Services.AddSingleton<IRepositoryProvider, LiteDbRepositoryProvider>();
        builder.Services.AddTransient(typeof(IRepository<>), typeof(LiteDbRepository<>));

        return builder;
    }

    private static string BuildConnectionString(LiteDbOptions opt)
    {
        var path = opt.DatabasePath ?? string.Empty;
        if (!path.Contains('='))
            path = "Filename=" + path;
        if (!string.IsNullOrEmpty(opt.Password))
            path += ";Password=" + opt.Password;
        if (!path.Contains("Connection=", StringComparison.OrdinalIgnoreCase)
            && !path.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
            path += ";Connection=shared";
        return path;
    }
}
