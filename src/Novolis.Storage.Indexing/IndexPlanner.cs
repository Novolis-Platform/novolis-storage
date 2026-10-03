using Novolis.Storage.Query;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Indexing;

/// <summary>Selects an index whose leading property has a finite query predicate.</summary>
public static class IndexPlanner
{
    public static IndexPlanDiagnostic Describe<T>(Query<T> query, IndexPlan? plan)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(query);
        return plan is null
            ? new IndexPlanDiagnostic(
                typeof(T),
                "UnboundedScan",
                null,
                0,
                candidateHydrationRequired: true,
                nativeOrdering: false)
            : new IndexPlanDiagnostic(
                typeof(T),
                "SecondaryIndex",
                plan.Index.Identity,
                plan.Predicates.Count,
                candidateHydrationRequired: true,
                nativeOrdering: false);
    }

    public static IndexPlan? Select<T>(
        Query<T> query,
        IReadOnlyList<IndexDescriptor> indexes)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(indexes);
        var best = indexes
            .Select(index => (Index: index, Predicates: Match(index, query.Predicates)))
            .Where(candidate => candidate.Predicates.Count > 0)
            .OrderByDescending(candidate => candidate.Predicates.Count)
            .ThenBy(candidate => candidate.Index.Identity, StringComparer.Ordinal)
            .FirstOrDefault();
        return best.Index is null ? null : new IndexPlan(best.Index, best.Predicates);
    }

    private static IReadOnlyList<QueryPredicate> Match(
        IndexDescriptor index,
        IReadOnlyList<QueryPredicate> predicates)
    {
        var matched = new List<QueryPredicate>();
        foreach (var property in index.Properties)
        {
            var predicate = predicates.FirstOrDefault(candidate =>
                candidate.Property == property);
            if (predicate is null)
                break;

            matched.Add(predicate);
            if (predicate.Operator is not QueryComparisonOperator.Is
                and not QueryComparisonOperator.IsNot)
                break;
        }

        return matched;
    }
}
