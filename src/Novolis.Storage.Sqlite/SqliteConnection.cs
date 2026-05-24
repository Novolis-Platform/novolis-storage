namespace Novolis.Storage.Sqlite;

/// <summary>
/// Configuration for SQLite database access.
/// </summary>
public class SqliteConnection
{
    /// <summary>ADO.NET connection string for the database file.</summary>
    public string? ConnectionString { get; set; }
}
