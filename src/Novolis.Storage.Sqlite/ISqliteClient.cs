using System.Data;

using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Sqlite;

public interface ISqliteClient : IDisposable
{
    Task EnsureTableExistsAsync<T>() where T : class, IKeyed, new();
    Task<DataTable> RunQueryAsync<T>(string query) where T : class, IKeyed, new();
    
    Task<int> RunNonQueryCommandAsync(string command);
}