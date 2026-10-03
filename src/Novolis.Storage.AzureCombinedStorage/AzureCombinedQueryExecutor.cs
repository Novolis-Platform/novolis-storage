using System.Globalization;
using Azure.Data.Tables;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Indexing;
using Novolis.Storage.Query;

namespace Novolis.Storage.AzureCombinedStorage;

internal static class AzureCombinedQueryExecutor
{
    public static async ValueTask<QueryPage<T>> ExecuteAsync<T>(
        AzureCombinedRepository<T> repository,
        Query<T> query,
        CancellationToken cancellationToken)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await repository.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        if (query.Predicates.Count == 0 && query.Limit is null)
            throw new QueryNotSupportedException(
                "Azure Combined Storage requires a finite Take or an indexed predicate. An unbounded scan is not performed.");

        var entities = await ReadMatchesAsync(repository, query, cancellationToken).ConfigureAwait(false);
        Sort(entities, query.Ordering);
        var offset = ReadOffset(query);
        var limit = query.Limit ?? Math.Max(0, entities.Count - offset);
        var page = entities.Skip(offset).Take(limit).ToArray();
        var nextOffset = offset + page.Length;
        var continuation = limit > 0 && nextOffset < entities.Count
            ? QueryContinuation.Create(QueryFingerprints.Compute(query), nextOffset)
            : null;
        return new QueryPage<T>(page, continuation);
    }

    public static async ValueTask<long> CountAsync<T>(
        AzureCombinedRepository<T> repository,
        Query<T> query,
        CancellationToken cancellationToken)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await repository.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        if (query.Predicates.Count == 0)
            throw new QueryNotSupportedException(
                "Azure Combined Storage cannot count an unbounded repository scan.");

        var entities = await ReadMatchesAsync(repository, query, cancellationToken).ConfigureAwait(false);
        return entities.Count;
    }

    private static async Task<List<T>> ReadMatchesAsync<T>(
        AzureCombinedRepository<T> repository,
        Query<T> query,
        CancellationToken cancellationToken)
        where T : class, IHasId
    {
        var plan = IndexPlanner.Select(query, repository.Indexes);
        if (plan is null)
        {
            if (query.Predicates.Count != 0)
                throw new QueryNotSupportedException(
                    "No query path can satisfy the requested predicates. Client-side evaluation is not performed.");
            var finite = await ReadFiniteManifestScanAsync(repository, query, cancellationToken).ConfigureAwait(false);
            repository.LogQuery(IndexPlanner.Describe(query, plan), finite.Count, finite.Count);
            return finite;
        }

        var ranges = BuildRanges(plan);
        var candidates = new Dictionary<string, (Guid Id, Guid Revision)>(StringComparer.Ordinal);
        foreach (var range in ranges)
        {
            var filter = BuildFilter(plan.Index, range);
            await foreach (var row in repository.IndexTable.QueryAsync<TableEntity>(
                filter,
                maxPerPage: repository.Options.MaxPerPage,
                select: ["EntityId", "Revision"],
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                var id = ReadGuid(row["EntityId"]);
                var revision = ReadGuid(row["Revision"]);
                candidates.TryAdd(id.ToString("D") + "|" + revision.ToString("D"), (id, revision));
            }
        }

        var results = new List<T>();
        foreach (var candidate in candidates.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await repository.ReadManifestAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
            if (manifest is null
                || manifest.Manifest.Deleted
                || manifest.Manifest.Revision != candidate.Revision)
                continue;

            var entity = await repository.ReadPayloadAsync(manifest.Manifest, cancellationToken).ConfigureAwait(false);
            if (Matches(entity, query.Predicates))
                results.Add(entity);
        }

        repository.LogQuery(IndexPlanner.Describe(query, plan), candidates.Count, results.Count);
        return results;
    }

    private static async Task<List<T>> ReadFiniteManifestScanAsync<T>(
        AzureCombinedRepository<T> repository,
        Query<T> query,
        CancellationToken cancellationToken)
        where T : class, IHasId
    {
        var results = new List<T>();
        await foreach (var manifest in repository.ReadManifestsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (manifest.Manifest.Deleted)
                continue;
            results.Add(await repository.ReadPayloadAsync(manifest.Manifest, cancellationToken).ConfigureAwait(false));
            if (query.Limit is int limit && results.Count >= limit)
                break;
        }

        return results;
    }

    private static IReadOnlyList<(string? Lower, string? LowerOperator, string? Upper, string? UpperOperator)> BuildRanges(
        IndexPlan plan)
    {
        var ranges = new List<(string? Lower, string? LowerOperator, string? Upper, string? UpperOperator)>();
        var prefix = string.Empty;
        foreach (var predicate in plan.Predicates)
        {
            switch (predicate.Operator)
            {
                case QueryComparisonOperator.Is:
                    prefix += IndexKeyEncoder.Encode(predicate.Values[0]) + "|";
                    continue;
                case QueryComparisonOperator.IsIn:
                    foreach (var value in predicate.Values)
                    {
                        var exact = prefix + IndexKeyEncoder.Encode(value) + "|";
                        ranges.Add((exact, "ge", exact + "~", "lt"));
                    }
                    return ranges;
                case QueryComparisonOperator.StartsWith:
                    if (predicate.Values[0] is not string text)
                        throw new QueryNotSupportedException("StartsWith requires a string property.");
                    var stringPrefix = prefix + IndexKeyEncoder.Encode(text);
                    ranges.Add((stringPrefix, "ge", stringPrefix + "~", "lt"));
                    return ranges;
                case QueryComparisonOperator.IsGreaterThan:
                    ranges.Add((prefix + IndexKeyEncoder.Encode(predicate.Values[0]) + "|~", "gt", null, null));
                    return ranges;
                case QueryComparisonOperator.IsGreaterThanOrEqual:
                    ranges.Add((prefix + IndexKeyEncoder.Encode(predicate.Values[0]) + "|", "ge", null, null));
                    return ranges;
                case QueryComparisonOperator.IsLessThan:
                    ranges.Add((null, null, prefix + IndexKeyEncoder.Encode(predicate.Values[0]) + "|", "lt"));
                    return ranges;
                case QueryComparisonOperator.IsLessThanOrEqual:
                    ranges.Add((null, null, prefix + IndexKeyEncoder.Encode(predicate.Values[0]) + "|~", "le"));
                    return ranges;
                case QueryComparisonOperator.IsBetween:
                    ranges.Add((
                        prefix + IndexKeyEncoder.Encode(predicate.Values[0]) + "|",
                        "ge",
                        prefix + IndexKeyEncoder.Encode(predicate.Values[1]) + "|~",
                        "le"));
                    return ranges;
                case QueryComparisonOperator.IsNot:
                default:
                    throw new QueryNotSupportedException(
                        $"The index cannot execute '{predicate.Operator}' without an unbounded scan.");
            }
        }

        ranges.Add((prefix, "ge", prefix + "~", "lt"));
        return ranges;
    }

    private static string BuildFilter(
        IndexDescriptor descriptor,
        (string? Lower, string? LowerOperator, string? Upper, string? UpperOperator) range)
    {
        var identity = descriptor.Identity + "|" + descriptor.Version;
        var partition = IndexKeyEncoder.EncodePath(new PropertyPath([identity]));
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partition}");
        if (range.Lower is not null)
            filter += " and " + RowFilter(range.LowerOperator!, range.Lower);
        if (range.Upper is not null)
            filter += " and " + RowFilter(range.UpperOperator!, range.Upper);
        return filter;
    }

    private static string RowFilter(string operation, string value) =>
        operation switch
        {
            "ge" => TableClient.CreateQueryFilter($"RowKey ge {value}"),
            "gt" => TableClient.CreateQueryFilter($"RowKey gt {value}"),
            "le" => TableClient.CreateQueryFilter($"RowKey le {value}"),
            "lt" => TableClient.CreateQueryFilter($"RowKey lt {value}"),
            _ => throw new QueryNotSupportedException($"Unsupported index range operation '{operation}'."),
        };

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
        var values = IndexValueAccessor.Read(entity, predicate.Property);
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
                    IndexValueAccessor.Read(left, term.Property).FirstOrDefault(),
                    IndexValueAccessor.Read(right, term.Property).FirstOrDefault());
                if (comparison != 0)
                    return term.Direction == QueryOrderDirection.Ascending ? comparison : -comparison;
            }

            return left.Id.CompareTo(right.Id);
        });
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

    private static Guid ReadGuid(object value) =>
        value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
}
