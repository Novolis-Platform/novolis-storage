using System.Data;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Sqlite;

internal sealed class SqliteRepository<T> : IRepository<T> where T : class, IHasId
{
    private static readonly string TableName = typeof(T).Name;
    private readonly ISqliteClient _sqliteClient;

    public SqliteRepository(ISqliteClient sqliteClient)
    {
        _sqliteClient = sqliteClient;
        _sqliteClient.EnsureTableExistsAsync<T>().GetAwaiter().GetResult();
    }

    public IEnumerable<T> All()
    {
        var dataTable = _sqliteClient.RunQueryAsync<T>($"SELECT * FROM {TableName}").GetAwaiter().GetResult();
        return MapRows(dataTable);
    }

    public async ValueTask<T?> TryGetAsync(Guid id, CancellationToken ct = default)
    {
        var query = $"SELECT * FROM {TableName} WHERE Id = '{id}'";
        try
        {
            var dataTable = await _sqliteClient.RunQueryAsync<T>(query).ConfigureAwait(false);
            if (dataTable.Rows.Count == 0)
                return null;

            return MapRow(dataTable.Rows[0], dataTable);
        }
        catch (SqliteException e)
        {
            throw new AggregateException($"Error running command: {query}", e);
        }
    }

    public async ValueTask UpsertAsync(T entity, CancellationToken ct = default)
    {
        var properties = typeof(T).GetProperties();
        var commandBuilder = new StringBuilder($"INSERT OR REPLACE INTO {TableName} (");
        foreach (var property in properties)
            commandBuilder.Append($"{property.Name}, ");

        commandBuilder.Remove(commandBuilder.Length - 2, 2);
        commandBuilder.Append(") VALUES (");

        foreach (var property in properties)
            commandBuilder.Append($"{FormatSqlValue(property.GetValue(entity))}, ");

        commandBuilder.Remove(commandBuilder.Length - 2, 2);
        commandBuilder.Append(')');

        await _sqliteClient.RunNonQueryCommandAsync(commandBuilder.ToString()).ConfigureAwait(false);
    }

    public async ValueTask<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var affected = await _sqliteClient
            .RunNonQueryCommandAsync($"DELETE FROM {TableName} WHERE Id = '{id}'")
            .ConfigureAwait(false);
        return affected > 0;
    }

    private static IEnumerable<T> MapRows(DataTable dataTable)
    {
        if (dataTable.Rows.Count == 0)
            yield break;

        foreach (DataRow row in dataTable.Rows)
            yield return MapRow(row, dataTable);
    }

    private static T MapRow(DataRow row, DataTable dataTable)
    {
        var entity = Activator.CreateInstance<T>() ?? throw new InvalidOperationException($"Could not create an instance of {typeof(T).Name}.");
        foreach (var property in typeof(T).GetProperties())
        {
            if (!dataTable.Columns.Contains(property.Name))
                continue;

            var index = dataTable.Columns.IndexOf(property.Name);
            var value = row[index];
            if (value == DBNull.Value)
                continue;

            entity.SetPropertyValue(property.PropertyType, property, value);
        }

        return entity;
    }

    private static object? FormatSqlValue(object? value) => value switch
    {
        null => "NULL",
        string s => $"'{s.Replace("'", "''")}'",
        Guid g => $"'{g}'",
        DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
        DateTimeOffset dto => $"'{dto:yyyy-MM-dd HH:mm:ss zz00}'",
        TimeSpan ts => $"'{ts:hh\\:mm\\:ss}'",
        bool b => b ? "1" : "0",
        _ => value
    };
}
