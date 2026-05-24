using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Json;

/// <summary>
/// <see cref="IRepository{T}"/> implementation that persists entities as JSON files.
/// </summary>
/// <typeparam name="T">Keyed entity type.</typeparam>
/// <param name="context">JSON storage context.</param>
public class JsonRepository<T>(JsonContext context) : IRepository<T>
    where T : class, IKeyed, new()
{
    private readonly JsonTable<T> _table = context.GetTable<T>();

    /// <inheritdoc />
    public IQueryable<T?> GetAll() => _table.GetAllAsync().ToBlockingEnumerable().AsQueryable();

    /// <inheritdoc />
    public Task AddAsync(T entity) => _table.SaveAsync(entity.Id, entity);

    /// <inheritdoc />
    public Task UpdateAsync(T entity) => _table.SaveAsync(entity.Id, entity);

    /// <inheritdoc />
    public Task DeleteAsync(Guid id) => _table.DeleteAsync(id);

    /// <inheritdoc />
    public Task<T?> GetByIdAsync(Guid id) => _table.GetAsync(id);
}
