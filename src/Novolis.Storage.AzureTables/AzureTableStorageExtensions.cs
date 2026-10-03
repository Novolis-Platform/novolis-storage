using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureTables;

/// <summary>Registers Azure Table Storage as the repository backend.</summary>
public static class AzureTableStorageExtensions
{
    /// <summary>
    /// Adds <see cref="TableServiceClient"/>, <see cref="IRepositoryProvider"/>, <see cref="IRepository{T}"/>,
    /// and <see cref="IAzureTableRepository{T}"/>.
    /// </summary>
    public static IStorageBuilder AddAzureTableProvider(
        this IStorageBuilder builder,
        Action<AzureTableOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new AzureTableOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("Azure Table connection string is required.", nameof(configure));

        if (options.MaxPerPage is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(configure), "Azure Table MaxPerPage must be between 1 and 1000.");

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(_ => new TableServiceClient(options.ConnectionString));
        builder.Services.AddSingleton<IRepositoryProvider, AzureTableRepositoryProvider>();
        builder.Services.AddTransient(typeof(IRepository<>), typeof(AzureTableRepository<>));
        builder.Services.AddTransient(typeof(IAzureTableRepository<>), typeof(AzureTableRepository<>));
        return builder;
    }
}
