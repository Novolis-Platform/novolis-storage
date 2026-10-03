using Novolis.Storage.Indexing;
using Novolis.Storage.Query;

namespace Novolis.Storage.Unit.Repositories;

public sealed class StorageIndexingTests
{
    [Test]
    public async Task Numeric_and_temporal_encodings_are_deterministic_and_sortable()
    {
        await Assert.That(IndexKeyEncoder.Encode(42)).IsEqualTo(IndexKeyEncoder.Encode(42));
        await Assert.That(string.Compare(
                IndexKeyEncoder.Encode(-1),
                IndexKeyEncoder.Encode(1),
                StringComparison.Ordinal))
            .IsLessThan(0);
        await Assert.That(string.Compare(
                IndexKeyEncoder.Encode(new DateOnly(2026, 1, 1)),
                IndexKeyEncoder.Encode(new DateOnly(2026, 2, 1)),
                StringComparison.Ordinal))
            .IsLessThan(0);
        await Assert.That(IndexKeyEncoder.Encode("Ada")).IsNotEqualTo(IndexKeyEncoder.Encode("ada"));
    }

    [Test]
    public async Task Planner_prefers_the_index_matching_the_longest_prefix()
    {
        var status = new PropertyPath([nameof(AzureTableRecord.Status)]);
        var count = new PropertyPath([nameof(AzureTableRecord.Count)]);
        var query = new QueryTestProvider()
            .Query()
            .Where(entity => entity.Status)
            .Is(AzureTableStatus.Ready)
            .And(entity => entity.Count)
            .IsGreaterThan(1);
        var indexes = new[]
        {
            new IndexDescriptor("status", [status]),
            new IndexDescriptor("status-count", [status, count]),
        };

        var plan = IndexPlanner.Select(query, indexes);

        await Assert.That(plan).IsNotNull();
        await Assert.That(plan!.Index.Identity).IsEqualTo("status-count");
        await Assert.That(plan.Predicates.Count).IsEqualTo(2);
        var diagnostic = IndexPlanner.Describe(query, plan);
        await Assert.That(diagnostic.Plan).IsEqualTo("SecondaryIndex");
        await Assert.That(diagnostic.CandidateHydrationRequired).IsTrue();
    }

    [Test]
    public async Task Nested_paths_are_discovered_and_read()
    {
        var paths = IndexPathCatalog.Discover(typeof(StorageNestedEntity));
        var code = paths.Single(path => path.Value == "Details.Code");
        var entity = new StorageNestedEntity
        {
            Id = Guid.NewGuid(),
            Details = new StorageNestedDetails { Code = "NO" },
        };

        await Assert.That(IndexValueAccessor.Read(entity, code).Single()).IsEqualTo("NO");
    }

    private sealed class QueryTestProvider : Novolis.Storage.Query.IRepositoryQueryProvider<AzureTableRecord>
    {
        public Query<AzureTableRecord> Query() => new(this);

        public ValueTask<QueryPage<AzureTableRecord>> ExecuteAsync(
            Query<AzureTableRecord> query,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new QueryPage<AzureTableRecord>([], null));

        public ValueTask<long> CountAsync(
            Query<AzureTableRecord> query,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0L);
    }
}
