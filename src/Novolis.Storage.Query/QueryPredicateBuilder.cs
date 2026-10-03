using System.Linq.Expressions;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Query;

/// <summary>Builds one comparison against a strongly typed property path.</summary>
public sealed class QueryPredicateBuilder<T, TValue>
    where T : class, IHasId
{
    private readonly Query<T> _source;
    private readonly PropertyPath _property;

    internal QueryPredicateBuilder(Query<T> source, Expression<Func<T, TValue>> selector)
    {
        _source = source;
        _property = PropertyPathParser.Parse(selector);
    }

    public Query<T> Is(TValue value) => Add(QueryComparisonOperator.Is, value);

    public Query<T> IsNot(TValue value) => Add(QueryComparisonOperator.IsNot, value);

    public Query<T> IsGreaterThan(TValue value) => Add(QueryComparisonOperator.IsGreaterThan, value);

    public Query<T> IsGreaterThanOrEqual(TValue value) => Add(QueryComparisonOperator.IsGreaterThanOrEqual, value);

    public Query<T> IsLessThan(TValue value) => Add(QueryComparisonOperator.IsLessThan, value);

    public Query<T> IsLessThanOrEqual(TValue value) => Add(QueryComparisonOperator.IsLessThanOrEqual, value);

    public Query<T> IsBetween(TValue lower, TValue upper) =>
        _source.Append(new QueryPredicate(_property, QueryComparisonOperator.IsBetween, [lower, upper]));

    public Query<T> IsIn(IEnumerable<TValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return _source.Append(new QueryPredicate(_property, QueryComparisonOperator.IsIn, values.Cast<object?>().ToArray()));
    }

    public Query<T> StartsWith(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return _source.Append(new QueryPredicate(_property, QueryComparisonOperator.StartsWith, [prefix]));
    }

    private Query<T> Add(QueryComparisonOperator operation, TValue value) =>
        _source.Append(new QueryPredicate(_property, operation, [value]));
}
