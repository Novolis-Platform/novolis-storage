using System.Collections;
using System.Linq.Expressions;

namespace Novolis.Storage.AzureTables;

internal sealed class AzureTableQuery<T> : IAzureTableQuery<T>
{
    private readonly AzureTableQueryProvider _provider;

    public AzureTableQuery(AzureTableQueryProvider provider, Expression? expression)
    {
        _provider = provider;
        Expression = expression ?? Expression.Constant(this);
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public IQueryProvider Provider => _provider;

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        _provider.ExecuteAsync<T>(Expression, cancellationToken).GetAsyncEnumerator(cancellationToken);

    public IEnumerator<T> GetEnumerator() =>
        _provider.ExecuteAsync<T>(Expression, CancellationToken.None).ToBlockingEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
