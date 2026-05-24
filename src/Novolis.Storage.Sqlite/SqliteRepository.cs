using System.Data;
using System.Reflection;
using System.Text;

using Novolis.Storage.Abstractions;

using Microsoft.Data.Sqlite;

namespace Novolis.Storage.Sqlite;

/// <summary>
/// <see cref="IRepository{T}"/> backed by SQLite via <see cref="ISqliteClient"/>.
/// </summary>
/// <typeparam name="T">Keyed entity type.</typeparam>
public class SqliteRepository<T> : IRepository<T> where T : class, IKeyed, new()
{
    private readonly ISqliteClient _sqliteClient;

    /// <summary>
    /// Ensures the entity table exists before use.
    /// </summary>
    /// <param name="sqliteClient">SQLite client.</param>
    public SqliteRepository(ISqliteClient sqliteClient)
    {
        _sqliteClient = sqliteClient;
        _sqliteClient.EnsureTableExistsAsync<T>().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public IQueryable<T?> GetAll()
    {
        var entities = _sqliteClient.RunQueryAsync<T>($"SELECT * FROM {typeof(T).Name}").Result;

        if (entities.Rows.Count == 0)
            return new List<T?>().AsQueryable();

        var result = new List<T>();

        foreach (DataRow row in entities.Rows)
        {
            var entity = new T();
            foreach (var property in entity.GetType().GetProperties())
            {
                var fieldName = property.Name;
                var fieldType = property.PropertyType;
                var fieldExists = entities.Columns.Contains(fieldName);

                if (!fieldExists)
                    continue;

                var index = entities.Columns.IndexOf(fieldName);
                var value = row[index];

                if (value == DBNull.Value)
                    continue;

                entity.SetPropertyValue(fieldType, property, value);
            }
            result.Add(entity);
        }

        return result.AsQueryable();
    }

    /// <inheritdoc />
    public Task AddAsync(T entity)
    {
        var properties = entity.GetType().GetProperties();
        var commandBuilder = new StringBuilder($"INSERT INTO {typeof(T).Name} (");
        foreach (var property in properties)
        {
            commandBuilder.Append($"{property.Name}, ");
        }
        commandBuilder.Remove(commandBuilder.Length - 2, 2);
        commandBuilder.Append(") VALUES (");
        foreach (var property in properties)
        {
            commandBuilder.Append($"{GetValue(entity, property)}, ");
        }
        commandBuilder.Remove(commandBuilder.Length - 2, 2);
        commandBuilder.Append(")");
        return _sqliteClient.RunNonQueryCommandAsync(commandBuilder.ToString());
    }

    private static object? GetValue(T entity, PropertyInfo property)
    {
        var value = property.GetValue(entity);
        return value switch
        {
            string => $"'{value}'",
            Guid => $"'{value}'",
            DateTime => $"'{value:yyyy-MM-dd HH:mm:ss}'",
            DateTimeOffset => $"'{value:yyyy-MM-dd HH:mm:ss zz00}'",
            TimeSpan => $"'{value:hh\\:mm\\:ss}'",
            _ => value
        };
    }

    /// <inheritdoc />
    public Task UpdateAsync(T entity)
    {
        var properties = entity.GetType().GetProperties();
        var commandBuilder = new StringBuilder($"UPDATE {typeof(T).Name} SET ");
        foreach (var property in properties)
        {
            commandBuilder.Append($"{property.Name} = {property.GetValue(entity)}, ");
        }
        commandBuilder.Remove(commandBuilder.Length - 2, 2);
        commandBuilder.Append($" WHERE Id = '{entity.Id}'");
        return _sqliteClient.RunNonQueryCommandAsync(commandBuilder.ToString());
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id) => _sqliteClient.RunNonQueryCommandAsync($"DELETE FROM {typeof(T).Name} WHERE Id = '{id}'");

    /// <inheritdoc />
    public async Task<T?> GetByIdAsync(Guid id)
    {
        var query = $"SELECT * FROM {typeof(T).Name} WHERE Id = '{id}'";
        try
        {
            var dataTable = await _sqliteClient.RunQueryAsync<T>(query);

            if (dataTable.Rows.Count == 0)
                return null;

            var entity = new T();

            foreach (var property in entity.GetType().GetProperties())
            {
                var fieldName = property.Name;
                var fieldType = property.PropertyType;
                var fieldExists = dataTable.Columns.Contains(fieldName);

                if (!fieldExists)
                    continue;

                var index = dataTable.Columns.IndexOf(fieldName);
                var value = dataTable.Rows[0][index];

                if (value == DBNull.Value)
                    continue;

                entity.SetPropertyValue(fieldType, property, value);
            }

            return entity;
        }
        catch (SqliteException e)
        {
            throw new AggregateException($"Error running command: {query}", e);
        }
    }
}
