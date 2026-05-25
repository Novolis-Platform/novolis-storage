using LiteDB;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.LiteDb;

internal sealed class LiteDbRepositoryProvider(ILiteDatabase db) : IRepositoryProvider
{
    public IRepository<T> Create<T>() where T : class, IHasId => new LiteDbRepository<T>(db);
}
