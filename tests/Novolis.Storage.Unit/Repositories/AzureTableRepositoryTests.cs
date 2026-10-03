using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableRepositoryTests
{
    [Before(Class)]
    public static Task StartAzuriteAsync() => AzuriteTableFixture.StartAsync();

    [After(Class)]
    public static Task StopAzuriteAsync() => AzuriteTableFixture.StopAsync();

    [Test]
    public async Task Upsert_get_replace_and_delete_roundtrip()
    {
        RequireLocalAzurite();
        using var host = await StartHostAsync();
        var repo = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();
        var id = Guid.NewGuid();
        await repo.UpsertAsync(new AzureTableRecord { Id = id, Name = "first", Active = true });
        await Assert.That((await repo.TryGetAsync(id))!.Name).IsEqualTo("first");

        await repo.UpsertAsync(new AzureTableRecord { Id = id, Name = "second", Active = true });
        await Assert.That((await repo.TryGetAsync(id))!.Name).IsEqualTo("second");
        await Assert.That(await repo.DeleteAsync(id)).IsTrue();
        await Assert.That(await repo.DeleteAsync(id)).IsFalse();
        await Assert.That(await repo.TryGetAsync(id)).IsNull();
    }

    [Test]
    public async Task All_and_query_follow_every_continuation_page()
    {
        RequireLocalAzurite();
        using var host = await StartHostAsync(maxPerPage: 2);
        var repo = host.Services.GetRequiredService<IAzureTableRepository<AzureTableRecord>>();
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
        await foreach (var row in repo.QueryAsync(e => e.Active))
            active.Add(row.Id);

        await Assert.That(active.Count).IsEqualTo(4);
        await Assert.That(active.ToHashSet().SetEquals(ids.Take(4))).IsTrue();
    }

    [Test]
    public async Task Query_escapes_strings_and_matches_guid_row_key_and_token()
    {
        RequireLocalAzurite();
        using var host = await StartHostAsync(maxPerPage: 1);
        var repo = host.Services.GetRequiredService<IAzureTableRepository<AzureTableRecord>>();
        var nastyId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var token = Guid.NewGuid();
        var nasty = "x' or PartitionKey eq 'row";
        var when = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await repo.UpsertAsync(new AzureTableRecord
        {
            Id = nastyId,
            Name = nasty,
            Token = token,
            When = when,
            Status = AzureTableStatus.Ready,
            Count = 5,
        });
        await repo.UpsertAsync(new AzureTableRecord { Id = otherId, Name = "safe", Count = 1, Status = AzureTableStatus.Draft });

        var byName = await CollectAsync(repo.QueryAsync(e => e.Name == nasty));
        await Assert.That(byName.Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });

        var byId = await CollectAsync(repo.QueryAsync(e => e.Id == nastyId));
        await Assert.That(byId.Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });

        var byToken = await CollectAsync(repo.QueryAsync(e => e.Token == token));
        await Assert.That(byToken.Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });

        var byStatus = await CollectAsync(repo.QueryAsync(e => e.Status == AzureTableStatus.Ready && e.Count > 2));
        await Assert.That(byStatus.Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });

        var byWhen = await CollectAsync(repo.QueryAsync(e => e.When == when));
        await Assert.That(byWhen.Select(row => row.Id).ToArray()).IsEquivalentTo(new[] { nastyId });
    }

    [Test]
    public async Task Linq_query_pages_projects_and_stops()
    {
        RequireLocalAzurite();
        using var host = await StartHostAsync(maxPerPage: 1);
        var repo = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();
        foreach (var index in Enumerable.Range(0, 4))
        {
            await repo.UpsertAsync(new AzureTableRecord
            {
                Id = Guid.NewGuid(),
                Name = "kept",
                Active = true,
                Count = index,
            });
        }

        await repo.UpsertAsync(new AzureTableRecord { Id = Guid.NewGuid(), Name = "no", Active = false, Count = 9 });

        var names = new List<string>();
        foreach (var name in repo.Query().Where(row => row.Active).Where(row => row.Count >= 0).Take(3).Select(row => row.Name))
            names.Add(name);

        await Assert.That(names.Count).IsEqualTo(3);
        await Assert.That(names.All(name => name == "kept")).IsTrue();

        var all = new List<AzureTableRecord>();
        await foreach (var row in repo.Query())
            all.Add(row);
        await Assert.That(all.Count).IsEqualTo(5);

        var missing = new List<AzureTableRecord>();
        await foreach (var row in repo.Query().Where(row => row.Name == "missing"))
            missing.Add(row);
        await Assert.That(missing.Count).IsEqualTo(0);

        var stopped = new List<AzureTableRecord>();
        await foreach (var row in repo.Query().Take(0))
            stopped.Add(row);
        await Assert.That(stopped.Count).IsEqualTo(0);

        using var enumerator = repo.Query().Where(row => row.Name == "no").GetEnumerator();
        await Assert.That(enumerator.MoveNext()).IsTrue();
        await Assert.That(((AzureTableRecord)enumerator.Current!).Name).IsEqualTo("no");
    }

    [Test]
    public async Task Upsert_null_is_rejected_before_the_service()
    {
        var repo = new AzureTableRepository<AzureTableRecord>(
            new TableServiceClient("UseDevelopmentStorage=true"),
            new AzureTableOptions { ConnectionString = "UseDevelopmentStorage=true" });
        await Assert.That(async () => await repo.UpsertAsync(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Tables_are_isolated_by_entity_type()
    {
        RequireLocalAzurite();
        using var host = await StartHostAsync();
        var records = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();
        var markers = host.Services.GetRequiredService<IRepository<AzureTableMarker>>();
        var id = Guid.NewGuid();
        await records.UpsertAsync(new AzureTableRecord { Id = id, Name = "record" });
        await markers.UpsertAsync(new AzureTableMarker { Id = id, Label = "marker" });

        await Assert.That(records.All().Single().Name).IsEqualTo("record");
        await Assert.That(markers.All().Single().Label).IsEqualTo("marker");
    }

    private static void RequireLocalAzurite()
    {
        Skip.Unless(
            !AzuriteTableFixture.IsContinuousIntegration,
            "Azure Table container tests run locally and are skipped in CI.");
        if (!AzuriteTableFixture.IsAvailable)
            throw new InvalidOperationException("Azurite container failed to start.", AzuriteTableFixture.StartupError);
    }

    private static async Task<IHost> StartHostAsync(int? maxPerPage = null)
    {
        var builder = Host.CreateApplicationBuilder();
        var prefix = Guid.NewGuid().ToString("N")[..8];
        builder.Services.AddStorage(b => b.AddAzureTableProvider(o =>
        {
            o.ConnectionString = AzuriteTableFixture.ConnectionString;
            o.TablePrefix = prefix;
            o.MaxPerPage = maxPerPage;
        }));
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static async Task<List<AzureTableRecord>> CollectAsync(IAsyncEnumerable<AzureTableRecord> rows)
    {
        var list = new List<AzureTableRecord>();
        await foreach (var row in rows)
            list.Add(row);
        return list;
    }
}
