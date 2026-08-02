namespace Novolis.Storage.Sqlite;

/// <summary>
/// Configuration for the SQLite storage provider.
/// </summary>
public sealed class SqliteOptions
{
    /// <summary>ADO.NET connection string for the database file.</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
