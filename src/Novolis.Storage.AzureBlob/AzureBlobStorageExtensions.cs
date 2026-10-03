using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureBlob;

/// <summary>Registers typed JSON blob containers backed by Azure Blob Storage.</summary>
public static class AzureBlobStorageExtensions
{
    /// <summary>
    /// Adds <see cref="BlobServiceClient"/>, <see cref="AzureBlobStorageOptions"/>, and
    /// <see cref="IBlobContainer{T}"/> services.
    /// </summary>
    public static IStorageBuilder AddAzureBlobStorage(
        this IStorageBuilder builder,
        Action<AzureBlobStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AzureBlobStorageOptions();
        configure(options);
        Validate(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(_ => CreateBlobService(options));
        builder.Services.AddTransient(typeof(IBlobContainer<>), typeof(AzureBlobContainer<>));
        return builder;
    }

    private static BlobServiceClient CreateBlobService(AzureBlobStorageOptions options)
    {
        var clientOptions = new BlobClientOptions(
            BlobClientOptions.ServiceVersion.V2023_11_03);
        return options.Credential is null
            ? new BlobServiceClient(options.ConnectionString!, clientOptions)
            : new BlobServiceClient(options.BlobServiceUri!, options.Credential, clientOptions);
    }

    private static void Validate(AzureBlobStorageOptions options)
    {
        if (options.Credential is null && string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException(
                "A connection string or token credential is required.",
                nameof(options));

        if (options.Credential is not null && options.BlobServiceUri is null)
            throw new ArgumentException(
                "BlobServiceUri is required with a token credential.",
                nameof(options));

        if (!AzureBlobNames.IsValidContainerName(options.ContainerName))
            throw new ArgumentException(
                "ContainerName must be 3-63 lowercase letters, digits, and hyphens, "
                + "and must begin and end with a letter or digit.",
                nameof(options));

        if (options.MaxPerPage is < 1 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(options.MaxPerPage));

        ArgumentNullException.ThrowIfNull(options.SerializerOptions);
    }
}
