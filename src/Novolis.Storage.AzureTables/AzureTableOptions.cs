namespace Novolis.Storage.AzureTables;

/// <summary>Configuration for the Azure Table storage provider.</summary>
public sealed class AzureTableOptions
{
    /// <summary>Storage account connection string, including the table endpoint when using Azurite.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Optional prefix mixed into the generated table name so hosts can isolate data.</summary>
    public string? TablePrefix { get; set; }

    /// <summary>
    /// Maximum entities per service page (1–1000). The repository still returns every page.
    /// </summary>
    public int? MaxPerPage { get; set; }
}
