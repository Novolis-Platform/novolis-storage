namespace Novolis.Storage.Json;

/// <summary>
/// Configuration for file-based JSON entity storage.
/// </summary>
public class JsonConnection
{
    /// <summary>Root folder containing per-type subdirectories and JSON files.</summary>
    public string JsonDataFolder { get; set; } = string.Empty;
}
