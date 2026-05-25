namespace Novolis.Storage.Abstractions;

/// <summary>
/// Repository contract for entities with a unique identifier.
/// </summary>
public interface IRepository<T> where T : class, IHasId
{
    IEnumerable<T> All();
    ValueTask<T?> TryGetAsync(Guid id, CancellationToken ct = default);
    ValueTask UpsertAsync(T entity, CancellationToken ct = default);
    ValueTask<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
