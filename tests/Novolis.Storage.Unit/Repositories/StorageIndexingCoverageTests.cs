using Novolis.Storage.Abstractions;
using Novolis.Storage.Indexing;
using Novolis.Storage.Query;

namespace Novolis.Storage.Unit.Repositories;

public sealed class StorageIndexingCoverageTests
{
    [Test]
    public async Task Key_encoding_covers_supported_scalar_families_and_bounds()
    {
        var values = new object?[]
        {
            null,
            "Ada",
            true,
            false,
            (byte)1,
            (ushort)2,
            (uint)3,
            (ulong)4,
            (sbyte)-1,
            (short)-2,
            -3,
            -4L,
            1.5f,
            -2.5d,
            12.34m,
            Guid.NewGuid(),
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local),
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(1)),
            new DateOnly(2026, 1, 2),
            new TimeOnly(3, 4, 5),
            AzureTableStatus.Ready,
            new byte[] { 1, 2, 3 },
            new FormattableValue(5),
            new PlainValue(),
        };

        foreach (var value in values)
            await Assert.That(IndexKeyEncoder.Encode(value)).IsNotNull();

        var path = new PropertyPath(["Details", "Code"]);
        await Assert.That(IndexKeyEncoder.EncodePath(path)).IsEqualTo("44657461696C732E436F6465");
        await Assert.That(IndexKeyEncoder.LowerBound("Ada")).IsEqualTo(IndexKeyEncoder.Encode("Ada") + "|");
        await Assert.That(IndexKeyEncoder.UpperBound("Ada")).IsEqualTo(IndexKeyEncoder.Encode("Ada") + "|~");
        await Assert.That(() => IndexKeyEncoder.EncodePath(null!)).Throws<ArgumentNullException>();
        await Assert.That(string.Compare(
                IndexKeyEncoder.Encode(-2L),
                IndexKeyEncoder.Encode(-1L),
                StringComparison.Ordinal))
            .IsLessThan(0);
        await Assert.That(string.Compare(
                IndexKeyEncoder.Encode(-1.0d),
                IndexKeyEncoder.Encode(1.0d),
                StringComparison.Ordinal))
            .IsLessThan(0);
    }

    [Test]
    public async Task Value_access_and_path_catalog_cover_nested_nulls_collections_and_rejections()
    {
        var entity = new AccessorModel
        {
            Id = Guid.NewGuid(),
            Details = null,
            Children =
            [
                new AccessorChild { Code = "A" },
                new AccessorChild { Code = "B" },
            ],
        };

        await Assert.That(IndexValueAccessor.Read(entity, new PropertyPath(["Details", "Code"])))
            .IsEquivalentTo(new object?[] { null });
        await Assert.That(IndexValueAccessor.Read(entity, new PropertyPath(["Children", "Code"])))
            .IsEquivalentTo(new object?[] { "A", "B" });
        await Assert.That(IndexValueAccessor.Read(entity, new PropertyPath(["Missing"]))).IsEmpty();
        await Assert.That(IndexValueAccessor.Read(entity, new PropertyPath(["WriteOnly"]))).IsEmpty();
        await Assert.That(IndexValueAccessor.Read(entity, new PropertyPath(["Item"]))).IsEmpty();
        await Assert.That(() => IndexValueAccessor.Read(null!, new PropertyPath(["Id"])))
            .Throws<ArgumentNullException>();
        await Assert.That(() => IndexValueAccessor.Read(entity, null!))
            .Throws<ArgumentNullException>();

        var paths = IndexPathCatalog.Discover(typeof(CatalogModel));
        await Assert.That(paths.Select(path => path.Value))
            .Contains("Child.Value");
        await Assert.That(() => IndexPathCatalog.Discover(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => IndexPathCatalog.Discover(typeof(CatalogModel), 0))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(IndexPathCatalog.Discover(typeof(CatalogModel), 1)).IsNotEmpty();

        foreach (var type in new[]
        {
            typeof(int?), typeof(string), typeof(bool), typeof(byte), typeof(sbyte),
            typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
            typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(Guid),
            typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly),
            typeof(byte[]), typeof(AzureTableStatus),
        })
            await Assert.That(IndexPathCatalog.IsScalar(type)).IsTrue();
        await Assert.That(IndexPathCatalog.IsScalar(typeof(AccessorModel))).IsFalse();
    }

    [Test]
    public async Task Descriptor_plan_and_diagnostics_validate_inputs_and_matching()
    {
        var status = new PropertyPath([nameof(AzureTableRecord.Status)]);
        var count = new PropertyPath([nameof(AzureTableRecord.Count)]);
        var query = new IndexingQueryProvider()
            .Query()
            .Where(entity => entity.Status)
            .IsNot(AzureTableStatus.Draft)
            .And(entity => entity.Count)
            .Is(2);
        var descriptor = new IndexDescriptor("status-count", [status, count]);
        var plan = IndexPlanner.Select(query, [descriptor]);

        await Assert.That(plan).IsNotNull();
        await Assert.That(plan!.Predicates.Count).IsEqualTo(2);
        await Assert.That(IndexPlanner.Select(query, [])).IsNull();
        await Assert.That(IndexPlanner.Describe(query, null).Plan).IsEqualTo("UnboundedScan");
        await Assert.That(IndexPlanner.Describe(query, plan).IndexIdentity).IsEqualTo("status-count");

        var rangeQuery = new IndexingQueryProvider()
            .Query()
            .Where(entity => entity.Status)
            .IsGreaterThan(AzureTableStatus.Draft);
        await Assert.That(IndexPlanner.Select(rangeQuery, [descriptor])!.Predicates.Count).IsEqualTo(1);
        await Assert.That(() => IndexPlanner.Select<AzureTableRecord>(null!, [descriptor]))
            .Throws<ArgumentNullException>();
        await Assert.That(() => IndexPlanner.Select(query, null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => IndexPlanner.Describe<AzureTableRecord>(null!, plan))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new IndexDescriptor("", [status])).Throws<ArgumentException>();
        await Assert.That(() => new IndexDescriptor("status", [], 1)).Throws<ArgumentException>();
        await Assert.That(() => new IndexDescriptor("status", [status], 0))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new IndexDescriptor("status", null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new IndexPlan(null!, [])).Throws<ArgumentNullException>();
        await Assert.That(() => new IndexPlan(descriptor, null!)).Throws<ArgumentNullException>();
        await Assert.That(() => new IndexPlanDiagnostic(
                null!,
                "plan",
                null,
                0,
                candidateHydrationRequired: true,
                nativeOrdering: false))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new IndexPlanDiagnostic(
                typeof(AzureTableRecord),
                null!,
                null,
                0,
                candidateHydrationRequired: true,
                nativeOrdering: false))
            .Throws<ArgumentNullException>();
    }

    private sealed class IndexingQueryProvider : IRepositoryQueryProvider<AzureTableRecord>
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

    private sealed class AccessorModel : IHasId
    {
        public Guid Id { get; set; }

        public AccessorDetails? Details { get; set; }

        public IReadOnlyList<AccessorChild> Children { get; set; } = [];

        public string WriteOnly
        {
            set { }
        }

        public string this[int index] => index.ToString();
    }

    private sealed class AccessorDetails
    {
        public string Code { get; set; } = string.Empty;
    }

    private sealed class AccessorChild
    {
        public string Code { get; set; } = string.Empty;
    }

    private sealed class CatalogModel : IHasId
    {
        public Guid Id { get; set; }

        public int Number { get; set; }

        public CatalogChild Child { get; set; } = new();

        public Dictionary<string, string> Map { get; set; } = [];

        public CatalogModel Self => this;

        public string this[int index] => index.ToString();

        public string WriteOnly
        {
            set { }
        }
    }

    private sealed class CatalogChild
    {
        public int Value { get; set; }
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
