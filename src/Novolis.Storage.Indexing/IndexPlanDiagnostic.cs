namespace Novolis.Storage.Indexing;

/// <summary>Stable-enough diagnostic view of an internal index planning decision.</summary>
public sealed record IndexPlanDiagnostic
{
    public IndexPlanDiagnostic(
        Type entityType,
        string plan,
        string? indexIdentity,
        int candidatePredicateCount,
        bool candidateHydrationRequired,
        bool nativeOrdering)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        IndexIdentity = indexIdentity;
        CandidatePredicateCount = candidatePredicateCount;
        CandidateHydrationRequired = candidateHydrationRequired;
        NativeOrdering = nativeOrdering;
    }

    public Type EntityType { get; }

    public string Plan { get; }

    public string? IndexIdentity { get; }

    public int CandidatePredicateCount { get; }

    public bool CandidateHydrationRequired { get; }

    public bool NativeOrdering { get; }
}
