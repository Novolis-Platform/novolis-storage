using System.Collections.Concurrent;
using LiteDB;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.LiteDb;

internal sealed class LiteDbRepository<T>(ILiteDatabase db) : IRepository<T> where T : class, IHasId
{
    private static readonly string CollectionName = typeof(T).Name;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> PerIdLocks = new();

    private static SemaphoreSlim GetLock(Guid id) => PerIdLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

    public IEnumerable<T> All()
    {
        var col = db.GetCollection<T>(CollectionName);
        return col.FindAll();
    }

    public ValueTask<T?> TryGetAsync(Guid id, CancellationToken ct = default)
    {
        var col = db.GetCollection<T>(CollectionName);
        var doc = col.FindById(id);
        return ValueTask.FromResult<T?>(doc);
    }

    public async ValueTask UpsertAsync(T entity, CancellationToken ct = default)
    {
        var id = entity.Id;
        var sem = GetLock(id);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var col = db.GetCollection<T>(CollectionName);
            col.Upsert(entity);
        }
        finally
        {
            sem.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var sem = GetLock(id);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var col = db.GetCollection<T>(CollectionName);
            return col.Delete(id);
        }
        finally
        {
            sem.Release();
        }
    }
}
