using Novolis.Storage.Query;

namespace Novolis.Storage.Indexing;

/// <summary>Provider-independent choice of a logical index for a query.</summary>
public sealed record IndexPlan
{
    public IndexPlan(IndexDescriptor index, IReadOnlyList<QueryPredicate> predicates)
    {
        Index = index ?? throw new ArgumentNullException(nameof(index));
        Predicates = predicates?.ToArray() ?? throw new ArgumentNullException(nameof(predicates));
    }

    public IndexDescriptor Index { get; }

    public IReadOnlyList<QueryPredicate> Predicates { get; }
}
