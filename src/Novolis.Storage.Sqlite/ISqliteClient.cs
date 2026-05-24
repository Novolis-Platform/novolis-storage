using System.Data;

using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Sqlite;

/// <summary>
/// Low-level SQLite command execution for Novolis repositories.
/// </summary>
public interface ISqliteClient : IDisposable
{
    /// <summary>
    /// Creates the table for <typeparamref name="T"/> when it does not exist.
    /// </summary>
    /// <typeparam name="T">Keyed entity type.</typeparam>
    Task EnsureTableExistsAsync<T>() where T : class, IKeyed, new();

    /// <summary>
    /// Runs a query and materializes results into a <see cref="DataTable"/>.
    /// </summary>
    /// <typeparam name="T">Entity type used for table naming.</typeparam>
    /// <param name="query">SQL query text.</param>
    /// <returns>Result table (may be empty).</returns>
    Task<DataTable> RunQueryAsync<T>(string query) where T : class, IKeyed, new();

    /// <summary>
    /// Executes a non-query SQL command.
    /// </summary>
    /// <param name="command">SQL command text.</param>
    /// <returns>Number of rows affected.</returns>
    Task<int> RunNonQueryCommandAsync(string command);
}
