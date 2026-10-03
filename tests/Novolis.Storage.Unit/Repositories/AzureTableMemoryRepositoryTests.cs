using System.Collections;
using Azure.Core.Pipeline;
using Azure.Data.Tables;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableMemoryRepositoryTests
{
    [Test]
    public async Task Upsert_get_replace_and_delete_roundtrip()
    {
        var handler = new AzureTableMemoryHandler();
        var repo = Repository<AzureTableRecord>(handler);
        var id = Guid.NewGuid();
        await repo.UpsertAsync(new AzureTableRecord { Id = id, Name = "first", Active = true });
        await Assert.That((await repo.TryGetAsync(id))!.Name).IsEqualTo("first");
        await repo.UpsertAsync(new AzureTableRecord { Id = id, Name = "second", Active = true });
        await Assert.That((await repo.TryGetAsync(id))!.Name).IsEqualTo("second");
        await Assert.That(await repo.DeleteAsync(id)).IsTrue();
        await Assert.That(await repo.DeleteAsync(id)).IsFalse();
        await Assert.That(await repo.TryGetAsync(id)).IsNull();
        await Assert.That(() => repo.QueryAsync(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task All_query_and_linq_follow_pages_and_stop()
    {
        var handler = new AzureTableMemoryHandler();
        var repo = Repository<AzureTableRecord>(handler, maxPerPage: 2);
        IAzureTableRepository<AzureTableRecord> tables = repo;
        var ids = Enumerable.Range(1, 5).Select(n => Guid.Parse($"00000000-0000-0000-0000-{n:D12}")).ToArray();
        foreach (var (id, index) in ids.Select((id, index) => (id, index)))
        {
            await repo.UpsertAsync(new AzureTableRecord
            {
                Id = id,
                Name = index < 4 ? "kept" : "other",
                Active = index < 4,
                Count = index,
            });
        }

        var all = repo.All().Select(row => row.Id).ToHashSet();
        await Assert.That(all.Count).IsEqualTo(5);
        await Assert.That(all.SetEquals(ids)).IsTrue();

        var active = new List<Guid>();
        await foreach (var row in tables.QueryAsync(e => e.Active))
            active.Add(row.Id);

        await Assert.That(active.Count).IsEqualTo(4);

        var names = new List<string>();
        foreach (var name in repo.Query().Where(row => row.Active).Where(row => row.Count >= 0).Take(3).Select(row => row.Name))
            names.Add(name);

        await Assert.That(names).IsEquivalentTo(new[] { "kept", "kept", "kept" });

        var everyone = new List<AzureTableRecord>();
        await foreach (var row in repo.Query())
            everyone.Add(row);
        await Assert.That(everyone.Count).IsEqualTo(5);

        var missing = new List<AzureTableRecord>();
        await foreach (var row in repo.Query().Where(row => row.Name == "missing"))
            missing.Add(row);
        await Assert.That(missing.Count).IsEqualTo(0);

        var stopped = new List<AzureTableRecord>();
        await foreach (var row in repo.Query().Take(0))
            stopped.Add(row);
        await Assert.That(stopped.Count).IsEqualTo(0);

        IEnumerable untyped = repo.Query().Where(row => row.Name == "other");
        var enumerator = untyped.GetEnumerator();
        try
        {
            await Assert.That(enumerator.MoveNext()).IsTrue();
            await Assert.That(((AzureTableRecord)enumerator.Current).Name).IsEqualTo("other");
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    [Test]
    public async Task Filters_escape_and_tables_stay_isolated()
    {
        var handler = new AzureTableMemoryHandler();
        var records = Repository<AzureTableRecord>(handler, maxPerPage: 1);
        var markers = Repository<AzureTableMarker>(handler);
        var nastyId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var token = Guid.NewGuid();
        var nasty = "x' or PartitionKey eq 'row";
        var when = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await records.UpsertAsync(new AzureTableRecord
        {
            Id = nastyId,
            Name = nasty,
            Token = token,
            When = when,
            Status = AzureTableStatus.Ready,
            Count = 5,
            Active = true,
        });
        await records.UpsertAsync(new AzureTableRecord { Id = otherId, Name = "safe", Count = 1, Status = AzureTableStatus.Draft });

        await Assert.That((await CollectAsync(records.QueryAsync(e => e.Name == nasty))).Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });
        await Assert.That((await CollectAsync(records.QueryAsync(e => e.Id == nastyId))).Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });
        await Assert.That((await CollectAsync(records.QueryAsync(e => e.Token == token))).Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });
        await Assert.That((await CollectAsync(records.QueryAsync(e => e.Status == AzureTableStatus.Ready && e.Count > 2))).Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });
        await Assert.That((await CollectAsync(records.QueryAsync(e => e.When == when))).Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });
        await Assert.That((await CollectAsync(records.QueryAsync(e => !e.Active))).Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { otherId });

        var shared = Guid.NewGuid();
        await records.UpsertAsync(new AzureTableRecord { Id = shared, Name = "record" });
        await markers.UpsertAsync(new AzureTableMarker { Id = shared, Label = "marker" });
        await Assert.That(records.All().Single(row => row.Id == shared).Name).IsEqualTo("record");
        await Assert.That(markers.All().Single().Label).IsEqualTo("marker");
    }

    private static AzureTableRepository<T> Repository<T>(AzureTableMemoryHandler handler, int? maxPerPage = null)
        where T : class, IHasId
    {
        var options = new TableClientOptions
        {
            Transport = new HttpClientTransport(handler),
        };
        options.Retry.MaxRetries = 0;
        var credential = new TableSharedKeyCredential(
            "devstoreaccount1",
            "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==");
        var service = new TableServiceClient(new Uri("https://novolis.table.core.windows.net"), credential, options);
        return new AzureTableRepository<T>(
            service,
            new AzureTableOptions
            {
                ConnectionString = Guid.NewGuid().ToString("N"),
                MaxPerPage = maxPerPage,
            });
    }

    private static async Task<List<AzureTableRecord>> CollectAsync(IAsyncEnumerable<AzureTableRecord> rows)
    {
        var list = new List<AzureTableRecord>();
        await foreach (var row in rows)
            list.Add(row);

        return list;
    }
}
