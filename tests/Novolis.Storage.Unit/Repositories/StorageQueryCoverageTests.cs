using System.Linq.Expressions;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Query;

namespace Novolis.Storage.Unit.Repositories;

public sealed class StorageQueryCoverageTests
{
    [Test]
    public async Task Query_terminals_and_capability_discovery_cover_direct_and_decorated_repositories()
    {
        var first = new AzureTableRecord { Id = Guid.NewGuid(), Name = "first" };
        var provider = new TestQueryProvider(first);
        var direct = new DirectRepository(provider);
        var decorated = new CapabilityRepository(provider);

        await Assert.That((await direct.Query().FirstOrDefaultAsync())!.Id).IsEqualTo(first.Id);
        await Assert.That(await direct.Query().AnyAsync()).IsTrue();
        await Assert.That(await direct.Query().CountAsync()).IsEqualTo(1);
        await Assert.That((await direct.Query().ToListAsync()).Count).IsEqualTo(1);
        await Assert.That(decorated.GetCapability<IRepositoryQueryProvider<AzureTableRecord>>()).IsEqualTo(provider);
        await Assert.That(decorated.GetCapability<IRepository<AzureTableRecord>>()).IsNull();

        var emptyProvider = new TestQueryProvider(null);
        var empty = new DirectRepository(emptyProvider);
        await Assert.That(await empty.Query().FirstOrDefaultAsync()).IsNull();
        await Assert.That(await empty.Query().AnyAsync()).IsFalse();

        await Assert.That(() => ((IRepository<AzureTableRecord>)null!).Query())
            .Throws<ArgumentNullException>();
        await Assert.That(() => new UnsupportedRepository().Query())
            .Throws<QueryNotSupportedException>();
        await Assert.That(() => ((IRepositoryCapabilityProvider)null!).GetCapability<IRepositoryQueryProvider<AzureTableRecord>>())
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Query_model_validates_paths_tokens_values_and_immutability()
    {
        var provider = new TestQueryProvider(null);
        var query = new Query<AzureTableRecord>(provider);
        var built = query
            .Where(entity => entity.Name).Is("Ada")
            .And(entity => entity.Count).IsNot(0)
            .And(entity => entity.Count).IsGreaterThan(0)
            .And(entity => entity.Count).IsGreaterThanOrEqual(0)
            .And(entity => entity.Count).IsLessThan(10)
            .And(entity => entity.Count).IsLessThanOrEqual(10)
            .And(entity => entity.Count).IsBetween(0, 10)
            .And(entity => entity.Count).IsIn([1, 2])
            .And(entity => entity.Name).StartsWith("A")
            .OrderBy(entity => entity.Count)
            .OrderByDescending(entity => entity.Name)
            .Take(5)
            .Take(2)
            .Continuation("next");

        await Assert.That(query.Predicates).IsEmpty();
        await Assert.That(built.Predicates.Count).IsEqualTo(9);
        await Assert.That(built.Ordering.Count).IsEqualTo(2);
        await Assert.That(built.Limit).IsEqualTo(2);
        await Assert.That(built.ContinuationToken).IsEqualTo("next");
        await Assert.That(built.WithContinuation(null).ContinuationToken).IsNull();

        await Assert.That(() => query.Take(-1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => query.Where(entity => entity.Name).IsIn(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => query.Where(entity => entity.Name).StartsWith(null!))
            .Throws<ArgumentNullException>();

        var path = new PropertyPath(["Name"]);
        await Assert.That(() => new PropertyPath((IReadOnlyList<string>)null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new PropertyPath([])).Throws<ArgumentException>();
        await Assert.That(() => new PropertyPath(["Name", " "])).Throws<ArgumentException>();
        await Assert.That(path.Equals(new PropertyPath(["Name"]))).IsTrue();
        await Assert.That(path.Equals(new PropertyPath(["Count"]))).IsFalse();
        await Assert.That(path.Equals(null)).IsFalse();
        var samePath = new PropertyPath(["Name"]);
        await Assert.That(path!.Value).IsEqualTo("Name");
        await Assert.That(path.ToString()).IsEqualTo("Name");
        await Assert.That(path.GetHashCode()).IsEqualTo(samePath.GetHashCode());

        await Assert.That(() => new QueryPredicate(
                null!,
                QueryComparisonOperator.Is,
                ["value"]))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new QueryPredicate(
                path,
                QueryComparisonOperator.Is,
                []))
            .Throws<ArgumentException>();
        await Assert.That(() => new QueryPredicate(
                path,
                QueryComparisonOperator.IsBetween,
                ["only"]))
            .Throws<ArgumentException>();
        await Assert.That(() => new QueryPredicate(
                path,
                QueryComparisonOperator.IsIn,
                []))
            .Throws<ArgumentException>();
        await Assert.That(() => new QueryPredicate(
                path,
                QueryComparisonOperator.Is,
                null!))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new QueryPage<AzureTableRecord>(null!, null))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new QueryOrdering(null!, QueryOrderDirection.Ascending))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Selector_parser_and_continuation_validation_reject_non_paths()
    {
        await Assert.That(() => PropertyPathParser.Parse<SelectorModel, string>(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => PropertyPathParser.Parse<SelectorModel, string>(entity => entity.Field))
            .Throws<QueryNotSupportedException>();
        await Assert.That(() => PropertyPathParser.Parse<SelectorModel, string>(entity => SelectorModel.StaticValue))
            .Throws<QueryNotSupportedException>();
        await Assert.That(() => PropertyPathParser.Parse<SelectorModel, string>(entity => entity[0]))
            .Throws<QueryNotSupportedException>();
        await Assert.That(() => PropertyPathParser.Parse<SelectorModel, string>(entity => entity.Name.ToLowerInvariant()))
            .Throws<QueryNotSupportedException>();

        var fingerprint = "fingerprint";
        var token = QueryContinuation.Create(fingerprint, 3);
        await Assert.That(QueryContinuation.TryRead(token, fingerprint, out var offset)).IsTrue();
        await Assert.That(offset).IsEqualTo(3);
        await Assert.That(QueryContinuation.TryRead(token, "other", out _)).IsFalse();
        await Assert.That(QueryContinuation.TryRead(string.Empty, fingerprint, out _)).IsFalse();
        await Assert.That(QueryContinuation.TryRead(token, string.Empty, out _)).IsFalse();
        await Assert.That(QueryContinuation.TryRead("*", fingerprint, out _)).IsFalse();
        await Assert.That(QueryContinuation.TryRead(
                Convert.ToBase64String("fingerprint|-1"u8.ToArray()),
                fingerprint,
                out _))
            .IsFalse();
        await Assert.That(QueryContinuation.TryRead(
                Convert.ToBase64String("fingerprint|not-an-int"u8.ToArray()),
                fingerprint,
                out _))
            .IsFalse();
        await Assert.That(() => QueryContinuation.Create(string.Empty, 0))
            .Throws<ArgumentException>();
        await Assert.That(() => QueryContinuation.Create(fingerprint, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Fingerprints_are_stable_for_null_strings_formattable_and_fallback_values()
    {
        var provider = new TestQueryProvider(null);
        var path = new PropertyPath(["Name"]);
        var query = new Query<AzureTableRecord>(
            provider,
            [
                new QueryPredicate(
                    path,
                    QueryComparisonOperator.IsIn,
                    [
                        null,
                        "Ada",
                        new FormattableValue(12),
                        new PlainValue(),
                    ]),
            ],
            [new QueryOrdering(path, QueryOrderDirection.Descending)],
            limit: 4);

        var fingerprint = QueryFingerprints.Compute(query);
        await Assert.That(fingerprint).IsEqualTo(QueryFingerprints.Compute(query));
        await Assert.That(fingerprint.Length).IsEqualTo(64);
        await Assert.That(() => QueryFingerprints.Compute<AzureTableRecord>(null!))
            .Throws<ArgumentNullException>();
    }

    private sealed class TestQueryProvider(AzureTableRecord? item) :
        IRepositoryQueryProvider<AzureTableRecord>
    {
        public Query<AzureTableRecord> Query() => new(this);

        public ValueTask<QueryPage<AzureTableRecord>> ExecuteAsync(
            Query<AzureTableRecord> query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new QueryPage<AzureTableRecord>(
                item is null ? [] : [item],
                null));
        }

        public ValueTask<long> CountAsync(
            Query<AzureTableRecord> query,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(item is null ? 0L : 1L);
    }

    private sealed class DirectRepository(TestQueryProvider provider) :
        IRepository<AzureTableRecord>,
        IRepositoryQueryProvider<AzureTableRecord>
    {
        public IEnumerable<AzureTableRecord> All() => [];

        public ValueTask<AzureTableRecord?> TryGetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AzureTableRecord?>(null);

        public ValueTask UpsertAsync(
            AzureTableRecord entity,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask<QueryPage<AzureTableRecord>> ExecuteAsync(
            Query<AzureTableRecord> query,
            CancellationToken cancellationToken = default) =>
            provider.ExecuteAsync(query, cancellationToken);

        public ValueTask<long> CountAsync(
            Query<AzureTableRecord> query,
            CancellationToken cancellationToken = default) =>
            provider.CountAsync(query, cancellationToken);
    }

    private sealed class CapabilityRepository(TestQueryProvider provider) :
        IRepository<AzureTableRecord>,
        IRepositoryCapabilityProvider
    {
        public IEnumerable<AzureTableRecord> All() => [];

        public ValueTask<AzureTableRecord?> TryGetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AzureTableRecord?>(null);

        public ValueTask UpsertAsync(
            AzureTableRecord entity,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public object? GetCapability(Type capabilityType) =>
            capabilityType == typeof(IRepositoryQueryProvider<AzureTableRecord>)
                ? provider
                : null;
    }

    private sealed class UnsupportedRepository : IRepository<AzureTableRecord>
    {
        public IEnumerable<AzureTableRecord> All() => [];

        public ValueTask<AzureTableRecord?> TryGetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AzureTableRecord?>(null);

        public ValueTask UpsertAsync(
            AzureTableRecord entity,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class SelectorModel : IHasId
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Field = string.Empty;

        public static string StaticValue => "static";

        public string this[int index] => index.ToString();
    }

    private sealed class FormattableValue(int value) : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            value.ToString(formatProvider);
    }

    private sealed class PlainValue
    {
        public override string ToString() => "plain";
    }
}
