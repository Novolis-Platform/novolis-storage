namespace Novolis.Storage.Abstractions;

/// <summary>
/// Resolves the appropriate <see cref="IRepository{T}"/> for a given entity type.
/// Closed-generic registrations override the default open-generic for that type.
/// </summary>
public interface IRepositoryFactory
{
    IRepository<T> Create<T>() where T : class, IHasId;
}
