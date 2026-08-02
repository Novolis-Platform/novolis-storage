using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Sqlite;

internal sealed class SqliteRepositoryProvider(ISqliteClient client) : IRepositoryProvider
{
    public IRepository<T> Create<T>() where T : class, IHasId => new SqliteRepository<T>(client);
}
