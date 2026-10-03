using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureCombinedStorage;

/// <summary>Registers preview aggregate storage over Azure Tables and Blobs.</summary>
public static class AzureCombinedStorageExtensions
{
    public static IStorageBuilder AddAzureCombinedStorage(
        this IStorageBuilder builder,
        Action<AzureCombinedStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new AzureCombinedStorageOptions();
        configure(options);
        Validate(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(_ => CreateTableService(options));
        builder.Services.AddSingleton(_ => CreateBlobService(options));
        builder.Services.AddSingleton<IRepositoryProvider, AzureCombinedRepositoryProvider>();
        builder.Services.AddTransient(typeof(IRepository<>), typeof(AzureCombinedRepository<>));
        builder.Services.AddTransient(typeof(IAzureCombinedMaintenance<>), typeof(AzureCombinedRepository<>));
        return builder;
    }

    private static TableServiceClient CreateTableService(AzureCombinedStorageOptions options)
    {
        return options.Credential is null
            ? new TableServiceClient(options.ConnectionString!)
            : new TableServiceClient(options.TableServiceUri!, options.Credential);
    }

    private static BlobServiceClient CreateBlobService(AzureCombinedStorageOptions options)
    {
        var clientOptions = new BlobClientOptions(
            BlobClientOptions.ServiceVersion.V2023_11_03);
        return options.Credential is null
            ? new BlobServiceClient(options.ConnectionString!, clientOptions)
            : new BlobServiceClient(options.BlobServiceUri!, options.Credential, clientOptions);
    }

    private static void Validate(AzureCombinedStorageOptions options)
    {
        if (options.Credential is null && string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException(
                "A connection string or token credential is required.",
                nameof(options));
        if (options.Credential is not null
            && (options.TableServiceUri is null || options.BlobServiceUri is null))
            throw new ArgumentException(
                "TableServiceUri and BlobServiceUri are required with a token credential.",
                nameof(options));
        if (options.ShardCount is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(options.ShardCount));
        if (options.MaxPerPage is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options.MaxPerPage));
        if (options.MaxParallelBlobReads is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(options.MaxParallelBlobReads));
        if (options.SchemaVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(options.SchemaVersion));
        if (string.IsNullOrWhiteSpace(options.BlobContainerName))
            throw new ArgumentException("A blob container name is required.", nameof(options));
    }
}
