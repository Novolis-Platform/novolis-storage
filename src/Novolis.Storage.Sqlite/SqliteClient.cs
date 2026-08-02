using System.Data;
using Microsoft.Data.Sqlite;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Sqlite.Internals;

namespace Novolis.Storage.Sqlite;

/// <summary>
/// SQLite client that ensures database files exist and runs commands.
/// </summary>
public sealed class SqliteClient : ISqliteClient
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly SqliteTypeMapper _sqliteTypeMapper = new();
    private bool _disposed;

    /// <summary>
    /// Opens or creates the database file from configuration.
    /// </summary>
    /// <param name="options">Connection options.</param>
    public SqliteClient(SqliteOptions options)
    {
        _connection = new(options.ConnectionString ?? throw new InvalidOperationException("Connection string is not set."));

        var databaseFilePath = _connection.DataSource;
        if (!string.IsNullOrWhiteSpace(databaseFilePath) && databaseFilePath != ":memory:")
        {
            var databaseDirectory = Path.GetDirectoryName(databaseFilePath);
            if (string.IsNullOrWhiteSpace(databaseDirectory))
                throw new InvalidOperationException("Database directory is not set.");

            if (!Directory.Exists(databaseDirectory))
                Directory.CreateDirectory(databaseDirectory);
        }

        _connection.Open();
    }

    private static string GetTableName<T>() where T : class, IHasId => typeof(T).Name;

    /// <inheritdoc />
    public async Task<DataTable> RunQueryAsync<T>(string query) where T : class, IHasId
    {
        await using var command = new SqliteCommand(query, _connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        var dataTable = new DataTable(GetTableName<T>());
        if (reader.HasRows)
            dataTable.Load(reader);

        return dataTable;
    }

    /// <inheritdoc />
    public async Task<int> RunNonQueryCommandAsync(string command)
    {
        try
        {
            await using var sqliteCommand = new SqliteCommand(command, _connection);
            return await sqliteCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (SqliteException e)
        {
            throw new AggregateException($"Error running command: {command}", e);
        }
    }

    /// <inheritdoc />
    public async Task EnsureTableExistsAsync<T>() where T : class, IHasId
    {
        var tableName = GetTableName<T>();
        var tableExistsQuery = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';";

        await using var tableExistsCommand = new SqliteCommand(tableExistsQuery, _connection);
        var tableExists = await tableExistsCommand.ExecuteScalarAsync().ConfigureAwait(false) != null;

        if (tableExists)
            return;

        var createTableStatement = _sqliteTypeMapper.CreateTableIfNotExistsStatement<T>();
        await using var createTableCommand = new SqliteCommand(createTableStatement, _connection);
        await createTableCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
