using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureCombinedStorage;
using Novolis.Storage.Query;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureCombinedStorageTests
{
    [Before(Class)]
    public static Task StartAzuriteAsync() => AzuriteTableFixture.StartAsync();

    [After(Class)]
    public static Task StopAzuriteAsync() => AzuriteTableFixture.StopAsync();

    [Test]
    public async Task Published_revisions_roundtrip_and_delete_as_tombstone()
    {
        RequireLocalAzurite();
        using var host = await StartHostAsync();
        var repository = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();
        var id = Guid.NewGuid();

        await repository.UpsertAsync(new AzureTableRecord
        {
            Id = id,
            Name = "first",
            Count = 1,
            Status = AzureTableStatus.Draft,
        });
        await Assert.That((await repository.TryGetAsync(id))!.Name).IsEqualTo("first");

        await repository.UpsertAsync(new AzureTableRecord
        {
            Id = id,
            Name = "second",
            Count = 2,
            Status = AzureTableStatus.Ready,
        });
        await Assert.That((await repository.TryGetAsync(id))!.Name).IsEqualTo("second");
        await Assert.That(repository.All().Single().Count).IsEqualTo(2);

        var maintenance = host.Services.GetRequiredService<IAzureCombinedMaintenance<AzureTableRecord>>();
        var report = await maintenance.VerifyAsync();
        await Assert.That(report.ActiveManifests).IsEqualTo(1);
        await Assert.That(report.MissingBlobs).IsEqualTo(0);
        await Assert.That(await maintenance.RebuildIndexesAsync()).IsGreaterThan(0);

        await Assert.That(await repository.DeleteAsync(id)).IsTrue();
        await Assert.That(await repository.TryGetAsync(id)).IsNull();
        await Assert.That(repository.All()).IsEmpty();
        await Assert.That(await repository.DeleteAsync(id)).IsFalse();
    }

    [Test]
    public async Task Indexed_query_validates_revision_and_rejects_unindexed_paths()
    {
        RequireLocalAzurite();
        using var host = await StartHostAsync(configure: options =>
        {
            options.AddIndex<AzureTableRecord>("status", entity => entity.Status);
        });
        var repository = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();
        var id = Guid.NewGuid();

        await repository.UpsertAsync(new AzureTableRecord
        {
            Id = id,
            Name = "same",
            Status = AzureTableStatus.Draft,
        });
        await repository.UpsertAsync(new AzureTableRecord
        {
            Id = id,
            Name = "same",
            Status = AzureTableStatus.Ready,
        });

        var ready = await repository
            .Query()
            .Where(entity => entity.Status)
            .Is(AzureTableStatus.Ready)
            .Take(10)
            .ToListAsync();
        await Assert.That(ready.Select(entity => entity.Id).ToArray()).IsEquivalentTo(new[] { id });

        var stale = await repository
            .Query()
            .Where(entity => entity.Status)
            .Is(AzureTableStatus.Draft)
            .Take(10)
            .ToListAsync();
        await Assert.That(stale).IsEmpty();

        await Assert.That(async () => await repository
                .Query()
                .Where(entity => entity.Name)
                .Is("same")
                .Take(10)
                .ToListAsync())
            .Throws<QueryNotSupportedException>();
    }

    [Test]
    public async Task In_process_transport_exercises_table_blob_query_and_repair_paths()
    {
        var handler = new AzureTableMemoryHandler();
        var repository = CreateRepository(handler);
        var id = Guid.NewGuid();
        await repository.UpsertAsync(new AzureTableRecord
        {
            Id = id,
            Name = "memory",
            Count = 2,
            Status = AzureTableStatus.Ready,
        });

        await Assert.That((await repository.TryGetAsync(id))!.Name).IsEqualTo("memory");
        var result = await repository
            .Query()
            .Where(entity => entity.Status)
            .Is(AzureTableStatus.Ready)
            .Take(1)
            .ToListAsync();
        await Assert.That(result.Select(entity => entity.Id).ToArray()).IsEquivalentTo(new[] { id });

        await Assert.That(await repository.RebuildIndexesAsync()).IsGreaterThan(0);
        await Assert.That((await repository.VerifyAsync()).MissingBlobs).IsEqualTo(0);
        await Assert.That(await repository.RemoveStaleIndexEntriesAsync()).IsEqualTo(0);
        await Assert.That(await repository.Query().Where(entity => entity.Count).Is(2).CountAsync()).IsEqualTo(1);
        await Assert.That((await repository.Query().Where(entity => entity.Count).IsIn(new[] { 1, 2 }).ToListAsync()).Count)
            .IsEqualTo(1);
        await Assert.That((await repository.Query().Where(entity => entity.Name).StartsWith("mem").ToListAsync()).Count)
            .IsEqualTo(1);
        await Assert.That((await repository.Query().Where(entity => entity.Count).IsGreaterThan(1).ToListAsync()).Count)
            .IsEqualTo(1);
        await Assert.That((await repository.Query().Where(entity => entity.Count).IsGreaterThanOrEqual(2).ToListAsync()).Count)
            .IsEqualTo(1);
        await Assert.That((await repository.Query().Where(entity => entity.Count).IsLessThan(3).ToListAsync()).Count)
            .IsEqualTo(1);
        await Assert.That((await repository.Query().Where(entity => entity.Count).IsLessThanOrEqual(2).ToListAsync()).Count)
            .IsEqualTo(1);
        await Assert.That((await repository.Query().Where(entity => entity.Count).IsBetween(1, 3).ToListAsync()).Count)
            .IsEqualTo(1);
        await Assert.That((await repository.Query().Take(1).ToListAsync()).Count).IsEqualTo(1);
        await Assert.That(async () => await repository.Query().ToPageAsync()).Throws<QueryNotSupportedException>();
        await Assert.That(async () => await repository.Query().CountAsync()).Throws<QueryNotSupportedException>();
        await Assert.That(async () => await repository.Query().Where(entity => entity.Name).IsNot("other").ToListAsync())
            .Throws<QueryNotSupportedException>();

        var secondId = Guid.NewGuid();
        await repository.UpsertAsync(new AzureTableRecord
        {
            Id = secondId,
            Name = "second",
            Count = 3,
            Status = AzureTableStatus.Ready,
        });
        await Assert.That((await repository.Query().Take(1).ToListAsync()).Count).IsEqualTo(1);
        await Assert.That(await repository.RemoveOrphansAsync(DateTimeOffset.UtcNow.AddMinutes(1)))
            .IsEqualTo(0);
        await Assert.That(await repository.DeleteAsync(id)).IsTrue();
        await Assert.That(await repository.TryGetAsync(id)).IsNull();
        await Assert.That((await repository.Query().Take(10).ToListAsync()).Select(entity => entity.Id))
            .IsEquivalentTo(new[] { secondId });
        await Assert.That((await repository.VerifyAsync()).ActiveManifests).IsEqualTo(1);
        await Assert.That(await repository.RebuildIndexesAsync()).IsGreaterThan(0);
        await Assert.That(await repository.RemoveOrphansAsync(DateTimeOffset.UtcNow.AddMinutes(1))).IsGreaterThan(0);
    }

    private static async Task<IHost> StartHostAsync(
        Action<AzureCombinedStorageOptions>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        var prefix = Guid.NewGuid().ToString("N")[..8];
        builder.Services.AddStorage(storage => storage.AddAzureCombinedStorage(options =>
        {
            options.ConnectionString = AzuriteTableFixture.ConnectionString;
            options.TablePrefix = prefix;
            options.ShardCount = 4;
            configure?.Invoke(options);
        }));
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static AzureCombinedRepository<AzureTableRecord> CreateRepository(
        AzureTableMemoryHandler handler)
    {
        var tableOptions = new TableClientOptions
        {
            Transport = new HttpClientTransport(handler),
        };
        tableOptions.Retry.MaxRetries = 0;
        var blobOptions = new BlobClientOptions
        {
            Transport = new HttpClientTransport(handler),
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

    private static void RequireLocalAzurite()
    {
        Skip.Unless(
            !AzuriteTableFixture.IsContinuousIntegration,
            "Azure Combined Storage container tests run locally and are skipped in CI.");
        Skip.Unless(
            AzuriteTableFixture.IsAvailable,
            "Azurite container is not running.");
    }
}
