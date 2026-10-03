using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Query;

/// <summary>Entry points for the optional finite query capability.</summary>
public static class QueryExtensions
{
    public static Query<T> Query<T>(this IRepository<T> repository)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(repository);
        var provider = repository as IRepositoryQueryProvider<T>
            ?? (repository as IRepositoryCapabilityProvider)
                ?.GetCapability<IRepositoryQueryProvider<T>>();
        return provider is null
            ? throw new QueryNotSupportedException(
                $"Repository '{repository.GetType().Name}' does not support Query<{typeof(T).Name}>. Client-side evaluation is not performed.")
            : new Query<T>(provider);
    }
}
