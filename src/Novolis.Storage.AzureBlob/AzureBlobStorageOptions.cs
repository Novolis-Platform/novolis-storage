using System.Text.Json;
using Azure.Core;

namespace Novolis.Storage.AzureBlob;

/// <summary>
/// Configuration for typed JSON values stored in Azure Blob Storage.
/// </summary>
public sealed class AzureBlobStorageOptions
{
    /// <summary>
    /// Development or emulator connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Blob service endpoint used with <see cref="Credential"/>.
    /// </summary>
    public Uri? BlobServiceUri { get; set; }

    /// <summary>
    /// Managed identity or another token credential for the blob service.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Physical container shared by the registered typed blob containers.
    /// </summary>
    public string ContainerName { get; set; } = "novolis-blobs";

    /// <summary>
    /// Maximum number of names requested in each Azure listing page.
    /// </summary>
    public int MaxPerPage { get; set; } = 100;

    /// <summary>
    /// JSON options used for every value written to or read from the container.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } =
        new(JsonSerializerDefaults.Web);
}
