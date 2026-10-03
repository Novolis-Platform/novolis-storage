using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Azure.Data.Tables;

namespace Novolis.Storage.AzureTables;

internal sealed class AzureTableQueryProvider : IQueryProvider
{
    private readonly Func<string, IReadOnlyList<string>?, CancellationToken, IAsyncEnumerable<TableEntity>> _query;
    private readonly Func<TableEntity, object> _materialize;

    public AzureTableQueryProvider(
        Func<string, IReadOnlyList<string>?, CancellationToken, IAsyncEnumerable<TableEntity>> query,
        Func<TableEntity, object> materialize)
    {
        _query = query;
        _materialize = materialize;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GenericTypeArguments[0];
        var queryType = typeof(AzureTableQuery<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryType, this, expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new AzureTableQuery<TElement>(this, expression);

    public object? Execute(Expression expression) => throw AzureTableFilterTranslator.Reject(expression.NodeType.ToString());

    public TResult Execute<TResult>(Expression expression) => throw AzureTableFilterTranslator.Reject(typeof(TResult).Name);

    public async IAsyncEnumerable<TElement> ExecuteAsync<TElement>(
        Expression expression,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = AzureTableQueryPlan.Parse(expression);
        var filter = plan.BuildFilter();
        var projector = plan.Selector?.Compile();
        var seen = 0;
        await foreach (var row in _query(filter, plan.Columns, cancellationToken).ConfigureAwait(false))
        {
            if (plan.Take is int limit && seen >= limit)
                yield break;

            var entity = _materialize(row);
            object? projected = projector is null ? entity : projector.DynamicInvoke(entity);
            seen++;
            yield return (TElement)projected!;
        }
    }
}
