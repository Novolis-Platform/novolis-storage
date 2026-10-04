namespace Novolis.Storage.AzureTables;

/// <summary>Configuration for the Azure Table storage provider.</summary>
public sealed class AzureTableOptions
{
    /// <summary>
    /// Storage account or Azurite connection string.
    /// An absolute http or https URI is treated as the table service endpoint and opened with the host's Azure credential.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Optional prefix mixed into the generated table name so hosts can isolate data.</summary>
    public string? TablePrefix { get; set; }

    /// <summary>
    /// Maximum entities per service page (1–1000). The repository still returns every page.
    /// </summary>
    public int? MaxPerPage { get; set; }
}
