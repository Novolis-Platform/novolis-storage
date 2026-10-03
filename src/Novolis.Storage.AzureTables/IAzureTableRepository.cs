using System.Linq.Expressions;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureTables;

/// <summary>
/// Azure Table repository. <see cref="QueryAsync"/> is an OData filter executed by the service,
/// followed across every continuation page.
/// </summary>
public interface IAzureTableRepository<T> : IRepository<T> where T : class, IHasId
{
    /// <summary>
    /// Returns every entity that matches <paramref name="predicate"/>.
    /// The predicate is translated to OData. Operators the Table service cannot evaluate throw
    /// <see cref="NotSupportedException"/> instead of being applied to a single page.
    /// </summary>
    IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}
