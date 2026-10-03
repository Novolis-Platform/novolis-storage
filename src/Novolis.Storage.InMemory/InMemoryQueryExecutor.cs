using System.Collections;
using System.Globalization;
using System.Reflection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Query;

namespace Novolis.Storage.InMemory;

internal static class InMemoryQueryExecutor
{
    public static ValueTask<QueryPage<T>> ExecuteAsync<T>(
        IEnumerable<T> source,
        Query<T> query,
        CancellationToken cancellationToken)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = source
            .Where(entity => Matches(entity, query.Predicates))
            .ToList();
        Sort(matches, query.Ordering);
        var offset = ReadOffset(query);
        var limit = query.Limit ?? Math.Max(0, matches.Count - offset);
        var items = matches.Skip(offset).Take(limit).ToArray();
        var nextOffset = offset + items.Length;
        var continuation = nextOffset < matches.Count
            ? QueryContinuation.Create(QueryFingerprints.Compute(query), nextOffset)
            : null;
        return ValueTask.FromResult(new QueryPage<T>(items, continuation));
    }

    public static ValueTask<long> CountAsync<T>(
        IEnumerable<T> source,
        Query<T> query,
        CancellationToken cancellationToken)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult((long)source.Count(entity => Matches(entity, query.Predicates)));
    }

    private static int ReadOffset<T>(Query<T> query) where T : class, IHasId
    {
        if (query.ContinuationToken is null)
            return 0;

        return QueryContinuation.TryRead(
            query.ContinuationToken,
            QueryFingerprints.Compute(query),
            out var offset)
            ? offset
            : throw new QueryNotSupportedException(
                "The continuation token does not belong to this query.");
    }

    private static bool Matches<T>(T entity, IReadOnlyList<QueryPredicate> predicates)
        where T : class, IHasId =>
        predicates.All(predicate => Matches(entity, predicate));

    private static bool Matches<T>(T entity, QueryPredicate predicate)
        where T : class, IHasId
    {
        var values = ReadValues(entity, predicate.Property);
        return predicate.Operator switch
        {
            QueryComparisonOperator.Is => values.Count == 0
                ? predicate.Values[0] is null
                : values.Any(value => Equal(value, predicate.Values[0])),
            QueryComparisonOperator.IsNot => values.Count == 0
                ? predicate.Values[0] is not null
                : values.All(value => !Equal(value, predicate.Values[0])),
            QueryComparisonOperator.IsGreaterThan => values.Any(value => Compare(value, predicate.Values[0]) > 0),
            QueryComparisonOperator.IsGreaterThanOrEqual => values.Any(value => Compare(value, predicate.Values[0]) >= 0),
            QueryComparisonOperator.IsLessThan => values.Any(value => Compare(value, predicate.Values[0]) < 0),
            QueryComparisonOperator.IsLessThanOrEqual => values.Any(value => Compare(value, predicate.Values[0]) <= 0),
            QueryComparisonOperator.IsBetween => values.Any(value =>
                Compare(value, predicate.Values[0]) >= 0
                && Compare(value, predicate.Values[1]) <= 0),
            QueryComparisonOperator.StartsWith => values.Any(value =>
                value is string text
                && predicate.Values[0] is string prefix
                && text.StartsWith(prefix, StringComparison.Ordinal)),
            QueryComparisonOperator.IsIn => values.Count == 0
                ? predicate.Values.Any(value => value is null)
                : values.Any(value => predicate.Values.Any(candidate => Equal(value, candidate))),
            _ => throw new QueryNotSupportedException($"Unsupported query operator '{predicate.Operator}'."),
        };
    }

    private static void Sort<T>(List<T> values, IReadOnlyList<QueryOrdering> ordering)
        where T : class, IHasId
    {
        values.Sort((left, right) =>
        {
            foreach (var term in ordering)
            {
                var comparison = Compare(
                    ReadValues(left, term.Property).FirstOrDefault(),
                    ReadValues(right, term.Property).FirstOrDefault());
                if (comparison != 0)
                    return term.Direction == QueryOrderDirection.Ascending ? comparison : -comparison;
            }

            return left.Id.CompareTo(right.Id);
        });
    }

    private static IReadOnlyList<object?> ReadValues(object entity, PropertyPath path)
    {
        var values = new List<object?>();
        Visit(entity, path.Segments, 0, values);
        return values;
    }

    private static void Visit(
        object? current,
        IReadOnlyList<string> segments,
        int index,
        List<object?> values)
    {
        if (current is null)
            return;
        if (index == segments.Count)
        {
            values.Add(current);
            return;
        }
        if (current is IEnumerable sequence and not string and not byte[])
        {
            foreach (var item in sequence)
                Visit(item, segments, index, values);
            return;
        }

        var property = current.GetType().GetProperty(
            segments[index],
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanRead || property.GetIndexParameters().Length != 0)
            return;
        Visit(property.GetValue(current), segments, index + 1, values);
    }

    private static bool Equal(object? left, object? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (IsNumber(left) && IsNumber(right))
            return Convert.ToDecimal(left, CultureInfo.InvariantCulture)
                == Convert.ToDecimal(right, CultureInfo.InvariantCulture);
        if (left is DateTime leftDate && right is DateTime rightDate)
            return leftDate.ToUniversalTime() == rightDate.ToUniversalTime();
        if (left is DateTimeOffset leftOffset && right is DateTimeOffset rightOffset)
            return leftOffset.UtcDateTime == rightOffset.UtcDateTime;
        return Equals(left, right);
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null && right is null)
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;
        if (IsNumber(left) && IsNumber(right))
            return Convert.ToDecimal(left, CultureInfo.InvariantCulture)
                .CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
        if (left is DateTime leftDate && right is DateTime rightDate)
            return leftDate.ToUniversalTime().CompareTo(rightDate.ToUniversalTime());
        if (left is DateTimeOffset leftOffset && right is DateTimeOffset rightOffset)
            return leftOffset.UtcDateTime.CompareTo(rightOffset.UtcDateTime);
        if (left is IComparable comparable && left.GetType() == right.GetType())
            return comparable.CompareTo(right);
        return string.Compare(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool IsNumber(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
