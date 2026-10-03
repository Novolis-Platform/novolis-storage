using System.Reflection;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureCombinedStorage;
using Novolis.Storage.Indexing;
using Novolis.Storage.Query;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureCombinedCoverageTests
{
    [Test]
    public async Task Names_keys_and_manifest_mapping_cover_normalized_physical_identifiers()
    {
        await Assert.That(AzureCombinedNames.BlobContainer("A_B", null)).IsEqualTo("abx");
        await Assert.That(AzureCombinedNames.BlobContainer(string.Empty, null)).IsEqualTo("xxx");
        await Assert.That(AzureCombinedNames.BlobContainer(new string('A', 70), "P_").Length)
            .IsEqualTo(62);
        await Assert.That(AzureCombinedNames.ManifestTable(typeof(AzureTableRecord), "123"))
            .StartsWith("T123");
        await Assert.That(AzureCombinedNames.IndexTable(typeof(AzureTableRecord), null))
            .EndsWith("Index");
        await Assert.That(AzureCombinedNames.ManifestTable(typeof(AzureTableRecord), new string('X', 70)).Length)
            .IsEqualTo(63);
        await Assert.That((string)typeof(AzureCombinedNames)
                .GetMethod("TableName", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, ["a"])!)
            .IsEqualTo("axx");
        await Assert.That(AzureCombinedNames.EntityStorage(typeof(AzureTableRecord)))
            .Contains("novolis");

        var id = Guid.NewGuid();
        var revision = Guid.NewGuid();
        await Assert.That(AzureCombinedKeys.Shard(id, 4)).StartsWith("s");
        await Assert.That(AzureCombinedKeys.RowKey(id)).IsEqualTo(id.ToString("D"));
        await Assert.That(AzureCombinedKeys.BlobName(typeof(AzureTableRecord), id, revision, 4))
            .EndsWith("/payload.json");
        await Assert.That(AzureCombinedKeys.IndexPartition("status|1"))
            .IsEqualTo("7374617475737C31");
        await Assert.That(AzureCombinedKeys.IndexRow("value", id, revision))
            .IsEqualTo($"value|{id:D}|{revision:D}");

        var manifest = new AzureCombinedManifest(
            id,
            "record",
            revision,
            2,
            Deleted: false,
            "record/payload.json",
            DateTimeOffset.UtcNow);
        var entity = AzureCombinedManifestMapper.ToEntity(
            manifest,
            typeof(AzureTableRecord),
            AzureCombinedKeys.Shard(id, 4));
        var roundTrip = AzureCombinedManifestMapper.Read(entity);
        await Assert.That(roundTrip.Manifest.Revision).IsEqualTo(revision);
        await Assert.That(roundTrip.Manifest.PublishedAt).IsEqualTo(manifest.PublishedAt);

        entity["Revision"] = revision.ToString("D");
        entity["PublishedAt"] = new DateTime(2026, 1, 2, 3, 4, 5);
        await Assert.That(AzureCombinedManifestMapper.Read(entity).Manifest.Revision).IsEqualTo(revision);
        entity["PublishedAt"] = manifest.PublishedAt.ToString("O");
        await Assert.That(AzureCombinedManifestMapper.Read(entity).Manifest.PublishedAt)
            .IsEqualTo(manifest.PublishedAt);
        await Assert.That(() => AzureCombinedSerializer.Deserialize<AzureTableRecord>(new BinaryData("null")))
            .Throws<InvalidOperationException>();
        await Assert.That(() => AzureCombinedSerializer.Deserialize<AzureTableRecord>(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Index_mapping_covers_empty_and_multiple_nested_combinations()
    {
        var entity = new IndexEntity
        {
            Id = Guid.NewGuid(),
            Children =
            [
                new IndexChild { Code = "A" },
                new IndexChild { Code = "B" },
            ],
            Tags =
            [
                new IndexTag { Value = "one" },
                new IndexTag { Value = "two" },
            ],
        };
        var descriptor = new IndexDescriptor(
            "children-tags",
            [
                new PropertyPath(["Children", "Code"]),
                new PropertyPath(["Tags", "Value"]),
            ]);
        var rows = AzureCombinedIndexMapper.ToEntities(
            entity,
            descriptor,
            entity.Id,
            Guid.NewGuid()).ToArray();
        await Assert.That(rows.Length).IsEqualTo(4);

        entity.Children = [];
        await Assert.That(AzureCombinedIndexMapper.ToEntities(
                entity,
                descriptor,
                entity.Id,
                Guid.NewGuid()))
            .IsEmpty();
    }

    [Test]
    public async Task Query_executor_helpers_cover_ranges_matching_sorting_and_comparison()
    {
        var path = new PropertyPath([nameof(AzureTableRecord.Count)]);
        var provider = new QueryExecutorProvider();
        var query = provider.Query();

        foreach (var operation in Enum.GetValues<QueryComparisonOperator>())
        {
            var rangeValues = operation switch
            {
                QueryComparisonOperator.IsBetween => new object?[] { 1, 3 },
                QueryComparisonOperator.IsIn => new object?[] { 1, 2 },
                QueryComparisonOperator.StartsWith => new object?[] { "A" },
                _ => new object?[] { 2 },
            };
            var predicate = new QueryPredicate(path, operation, rangeValues);
            var plan = new IndexPlan(new IndexDescriptor("count", [path]), [predicate]);
            if (operation is QueryComparisonOperator.IsNot)
            {
                await Assert.That(() => Invoke("BuildRanges", plan))
                    .Throws<TargetInvocationException>();
            }
            else
            {
                await Assert.That(Invoke("BuildRanges", plan)).IsNotNull();
            }
        }

        var startsWithInvalid = new QueryPredicate(
            new PropertyPath([nameof(AzureTableRecord.Name)]),
            QueryComparisonOperator.StartsWith,
            [42]);
        await Assert.That(() => Invoke(
                "BuildRanges",
                new IndexPlan(
                    new IndexDescriptor("name", [startsWithInvalid.Property]),
                    [startsWithInvalid])))
            .Throws<TargetInvocationException>();
        await Assert.That(() => Invoke("RowFilter", "bad", "x"))
            .Throws<TargetInvocationException>();

        var entity = new AzureTableRecord
        {
            Id = Guid.NewGuid(),
            Name = "Ada",
            Note = null,
            Count = 2,
            When = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        };
        foreach (var operation in new[]
        {
            QueryComparisonOperator.Is,
            QueryComparisonOperator.IsNot,
            QueryComparisonOperator.IsGreaterThan,
            QueryComparisonOperator.IsGreaterThanOrEqual,
            QueryComparisonOperator.IsLessThan,
            QueryComparisonOperator.IsLessThanOrEqual,
            QueryComparisonOperator.IsBetween,
            QueryComparisonOperator.StartsWith,
            QueryComparisonOperator.IsIn,
        })
        {
            var property = operation == QueryComparisonOperator.StartsWith
                ? new PropertyPath([nameof(AzureTableRecord.Name)])
                : path;
            var matchValues = operation switch
            {
                QueryComparisonOperator.IsBetween => new object?[] { 1, 3 },
                QueryComparisonOperator.IsIn => new object?[] { 1, 2 },
                QueryComparisonOperator.StartsWith => new object?[] { "A" },
                _ => new object?[] { 2 },
            };
            var predicate = new QueryPredicate(property, operation, matchValues);
            await Assert.That(Invoke("Matches", entity, predicate)).IsNotNull();
        }

        var emptyValues = new QueryPredicate(
            new PropertyPath(["Children", "Code"]),
            QueryComparisonOperator.IsIn,
            ["not-null"]);
        await Assert.That((bool)Invoke(
                "Matches",
                new IndexEntity { Id = Guid.NewGuid(), Children = [] },
                emptyValues)!)
            .IsFalse();
        await Assert.That((bool)Invoke(
                "Matches",
                new IndexEntity { Id = Guid.NewGuid(), Children = [] },
                new QueryPredicate(
                    new PropertyPath(["Children", "Code"]),
                    QueryComparisonOperator.Is,
                    [null]))!)
            .IsTrue();
        await Assert.That((bool)Invoke(
                "Matches",
                new IndexEntity { Id = Guid.NewGuid(), Children = [] },
                new QueryPredicate(
                    new PropertyPath(["Children", "Code"]),
                    QueryComparisonOperator.IsNot,
                    ["value"]))!)
            .IsTrue();

        var unsupported = new QueryPredicate(
            path,
            (QueryComparisonOperator)999,
            [2]);
        await Assert.That(() => Invoke("Matches", entity, unsupported))
            .Throws<TargetInvocationException>();

        var sorted = new List<AzureTableRecord>
        {
            entity,
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Ada",
                Count = 2,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Bob",
                Count = 1,
            },
        };
        Invoke(
            "Sort",
            sorted,
            new[]
            {
                new QueryOrdering(path, QueryOrderDirection.Ascending),
                new QueryOrdering(new PropertyPath([nameof(AzureTableRecord.Name)]), QueryOrderDirection.Descending),
            });
        await Assert.That(sorted[0].Count).IsEqualTo(1);

        foreach (var number in new object[] { (byte)1, (sbyte)1, (short)1, (ushort)1, 1, 1U, 1L, 1UL, 1F, 1D, 1M })
        {
            await Assert.That((bool)Invoke("Equal", number, number)!).IsTrue();
            await Assert.That(Invoke("Compare", number, number)).IsEqualTo(0);
        }

        await Assert.That((bool)Invoke("Equal", null, null)!).IsTrue();
        await Assert.That((bool)Invoke(
                "Equal",
                new DateTime(2026, 1, 1),
                new DateTime(2026, 1, 1, 1, 0, 0))!)
            .IsFalse();
        await Assert.That((bool)Invoke(
                "Equal",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2025, 12, 31, 19, 0, 0, TimeSpan.FromHours(-5)))!)
            .IsTrue();
        await Assert.That((bool)Invoke("Equal", null, 1)!).IsFalse();
        await Assert.That((bool)Invoke("Equal", 1, null)!).IsFalse();
        await Assert.That(Invoke("Compare", null, null)).IsEqualTo(0);
        await Assert.That((int)Invoke("Compare", null, 1)!).IsLessThan(0);
        await Assert.That((int)Invoke("Compare", 1, null)!).IsGreaterThan(0);
        await Assert.That((int)Invoke("Compare", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2))!)
            .IsLessThan(0);
        await Assert.That((int)Invoke(
                "Compare",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero))!)
            .IsLessThan(0);
        await Assert.That((int)Invoke("Compare", "a", "b")!).IsLessThan(0);
        await Assert.That((int)Invoke("Compare", "a", 1)!).IsNotEqualTo(0);
        var readId = Guid.NewGuid();
        await Assert.That(Invoke("ReadGuid", readId)).IsEqualTo(readId);
        await Assert.That(Invoke("ReadGuid", readId.ToString("D"))).IsEqualTo(readId);

        var continuationQuery = query.Where(entity => entity.Count).Is(2).Take(1);
        var token = QueryContinuation.Create(QueryFingerprints.Compute(continuationQuery), 1);
        await Assert.That(Invoke("ReadOffset", continuationQuery.WithContinuation(token))).IsEqualTo(1);
        await Assert.That(() => Invoke("ReadOffset", continuationQuery.WithContinuation("bad")))
            .Throws<TargetInvocationException>();
    }

    [Test]
    public async Task Registration_supports_token_credentials_provider_creation_and_validation()
    {
        var credential = new StaticCredential();
        var services = new ServiceCollection();
        services.AddStorage(builder => builder.AddAzureCombinedStorage(options =>
        {
            options.Credential = credential;
            options.TableServiceUri = new Uri("https://table.example");
            options.BlobServiceUri = new Uri("https://blob.example");
        }));
        using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<TableServiceClient>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<BlobServiceClient>()).IsNotNull();
        var repositoryProvider = provider.GetRequiredService<IRepositoryProvider>();
        await Assert.That(repositoryProvider.Create<AzureTableRecord>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IRepository<AzureTableRecord>>()).IsNotNull();

        await Assert.That(() => new ServiceCollection().AddStorage(builder =>
            builder.AddAzureCombinedStorage(null!)))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new ServiceCollection().AddStorage(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new ServiceCollection().AddStorage(builder =>
            builder.AddAzureCombinedStorage(options => options.Credential = credential)))
            .Throws<ArgumentException>();

        var options = new AzureCombinedStorageOptions();
        await Assert.That(() => options.AddIndex<AzureTableRecord>("empty"))
            .Throws<ArgumentException>();
        await Assert.That(options.GetIndexes(typeof(AzureTableRecord))).IsNotEmpty();
    }

    [Test]
    public async Task Maintenance_detects_missing_and_malformed_state()
    {
        var handler = new AzureTableMemoryHandler { DelayRequests = true };
        var repository = CreateRepository(handler);
        await Assert.That(() => new AzureCombinedRepository<AzureTableRecord>(
                null!,
                new BlobServiceClient("UseDevelopmentStorage=true"),
                new AzureCombinedStorageOptions()))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new AzureCombinedRepository<AzureTableRecord>(
                new TableServiceClient("UseDevelopmentStorage=true"),
                null!,
                new AzureCombinedStorageOptions()))
            .Throws<ArgumentNullException>();
        await Assert.That(() => new AzureCombinedRepository<AzureTableRecord>(
                new TableServiceClient("UseDevelopmentStorage=true"),
                new BlobServiceClient("UseDevelopmentStorage=true"),
                null!))
            .Throws<ArgumentNullException>();
        var firstReady = repository.EnsureReadyAsync(CancellationToken.None).AsTask();
        var secondReady = repository.EnsureReadyAsync(CancellationToken.None).AsTask();
        await Task.WhenAll(firstReady, secondReady);
        var id = Guid.NewGuid();
        await Assert.That(await repository.TryGetAsync(Guid.NewGuid())).IsNull();
        await Assert.That(await repository.DeleteAsync(Guid.NewGuid())).IsFalse();
        await repository.UpsertAsync(new AzureTableRecord { Id = id, Name = "active", Count = 1 });
        _ = repository.ManifestTable;
        var snapshot = await repository.ReadManifestAsync(id, CancellationToken.None);
        await repository.BlobContainer.DeleteBlobIfExistsAsync(
            snapshot!.Manifest.BlobName,
            cancellationToken: CancellationToken.None);
        var report = await repository.VerifyAsync();
        await Assert.That(report.ActiveManifests).IsEqualTo(1);
        await Assert.That(report.MissingBlobs).IsEqualTo(1);

        var malformed = new TableEntity("bad", "row");
        await repository.IndexTable.UpsertEntityAsync(malformed, TableUpdateMode.Replace);
        await Assert.That(await repository.RemoveStaleIndexEntriesAsync()).IsGreaterThan(0);
    }

    private static object? Invoke(string name, params object?[] arguments)
    {
        var methods = typeof(AzureCombinedQueryExecutor)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name == name)
            .ToArray();
        var method = methods.Single(candidate =>
            candidate.GetParameters().Length == arguments.Length
            && (name != "Matches"
                || candidate.GetParameters()[1].ParameterType != typeof(IReadOnlyList<QueryPredicate>)));
        if (method.IsGenericMethodDefinition)
            method = method.MakeGenericMethod(
                arguments.FirstOrDefault(argument => argument is IHasId)?.GetType()
                ?? typeof(AzureTableRecord));
        return method.Invoke(null, arguments);
    }

    private static AzureCombinedRepository<AzureTableRecord> CreateRepository(
        AzureTableMemoryHandler handler)
    {
        var tableOptions = new TableClientOptions
        {
            Transport = new Azure.Core.Pipeline.HttpClientTransport(handler),
        };
        tableOptions.Retry.MaxRetries = 0;
        var blobOptions = new BlobClientOptions
        {
            Transport = new Azure.Core.Pipeline.HttpClientTransport(handler),
        };
        blobOptions.Retry.MaxRetries = 0;
        var tableCredential = new TableSharedKeyCredential(
            "devstoreaccount1",
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==");
        var blobCredential = new StorageSharedKeyCredential(
            "devstoreaccount1",
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==");
        var tables = new TableServiceClient(
            new Uri("https://novolis.table.core.windows.net"),
            tableCredential,
            tableOptions);
        var blobs = new BlobServiceClient(
            new Uri("https://novolis.blob.core.windows.net"),
            blobCredential,
            blobOptions);
        return new AzureCombinedRepository<AzureTableRecord>(
            tables,
            blobs,
            new AzureCombinedStorageOptions
            {
                ConnectionString = "memory",
                TablePrefix = Guid.NewGuid().ToString("N")[..8],
                ShardCount = 4,
            });
    }

    private sealed class QueryExecutorProvider : IRepositoryQueryProvider<AzureTableRecord>
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

    private sealed class IndexEntity : IHasId
    {
        public Guid Id { get; set; }

        public List<IndexChild> Children { get; set; } = [];

        public List<IndexTag> Tags { get; set; } = [];
    }

    private sealed class IndexChild
    {
        public string Code { get; set; } = string.Empty;
    }

    private sealed class IndexTag
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class StaticCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
