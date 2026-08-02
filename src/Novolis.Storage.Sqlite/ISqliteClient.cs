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
    Task EnsureTableExistsAsync<T>() where T : class, IHasId;

    /// <summary>
    /// Runs a query and materializes results into a <see cref="DataTable"/>.
    /// </summary>
    Task<DataTable> RunQueryAsync<T>(string query) where T : class, IHasId;

    /// <summary>
    /// Executes a non-query SQL command.
    /// </summary>
    Task<int> RunNonQueryCommandAsync(string command);
}
