using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Json;

/// <summary>
/// Optional extension: generate id and persist in one step (immutable-record friendly).
/// </summary>
public static class RepositoryCreateExtensions
{
    /// <summary>
    /// Creates a new id via <paramref name="idProvider"/>, builds the entity with <paramref name="factory"/>, upserts it, and returns it.
    /// </summary>
    public static async ValueTask<T> CreateAsync<T>(
        this IRepository<T> repo,
        IIdProvider idProvider,
        Func<Guid, T> factory,
        CancellationToken ct = default)
        where T : class, IHasId
    {
        var id = idProvider.NewId();
        var entity = factory(id);
        await repo.UpsertAsync(entity, ct).ConfigureAwait(false);
        return entity;
    }
}
