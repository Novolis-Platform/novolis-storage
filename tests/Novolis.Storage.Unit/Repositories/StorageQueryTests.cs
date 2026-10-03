using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.InMemory;
using Novolis.Storage.Query;

namespace Novolis.Storage.Unit.Repositories;

public sealed class StorageQueryTests
{
    [Test]
    public async Task In_memory_query_supports_predicates_ordering_and_continuation()
    {
        using var host = BuildHost();
        var repository = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();
        var ids = new List<Guid>();
        foreach (var (name, count, status) in new[]
        {
            ("Ada", 3, AzureTableStatus.Ready),
            ("Ada", 1, AzureTableStatus.Ready),
            ("Bob", 2, AzureTableStatus.Draft),
            ("Ada", 4, AzureTableStatus.Ready),
        })
        {
            var entity = new AzureTableRecord
            {
                Id = Guid.NewGuid(),
                Name = name,
                Count = count,
                Status = status,
                Note = count == 1 ? null : "present",
            };
            ids.Add(entity.Id);
            await repository.UpsertAsync(entity);
        }

        var query = repository
            .Query()
            .Where(entity => entity.Status).Is(AzureTableStatus.Ready)
            .And(entity => entity.Name).StartsWith("A")
            .OrderByDescending(entity => entity.Count)
            .Take(2);

        var first = await query.ToPageAsync();
        await Assert.That(first.Items.Select(entity => entity.Count).ToArray()).IsEquivalentTo(new[] { 4, 3 });
        await Assert.That(first.ContinuationToken).IsNotNull();

        var second = await query.WithContinuation(first.ContinuationToken).ToPageAsync();
        await Assert.That(second.Items.Select(entity => entity.Count).ToArray()).IsEquivalentTo(new[] { 1 });
        await Assert.That(second.ContinuationToken).IsNull();
        await Assert.That(await query.CountAsync()).IsEqualTo(3);
    }

    [Test]
    public async Task In_memory_query_supports_ranges_sets_nulls_and_all_pages()
    {
        using var host = BuildHost();
        var repository = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();
        await repository.UpsertAsync(new AzureTableRecord { Id = Guid.NewGuid(), Name = "Ada", Count = 1 });
        await repository.UpsertAsync(new AzureTableRecord { Id = Guid.NewGuid(), Name = "Ada", Count = 2 });
        await repository.UpsertAsync(new AzureTableRecord { Id = Guid.NewGuid(), Name = "Eli", Count = 3, Note = "x" });

        var between = repository.Query().Where(entity => entity.Count).IsBetween(1, 2);
        await Assert.That((await between.ToListAsync()).Count).IsEqualTo(2);

        var selected = repository.Query().Where(entity => entity.Count).IsIn(new[] { 1, 3 });
        await Assert.That((await selected.ToListAsync()).Count).IsEqualTo(2);

        var missing = repository.Query().Where(entity => entity.Note).Is((string?)null);
        await Assert.That((await missing.ToListAsync()).Count).IsEqualTo(2);

        var all = repository.Query().Take(2);
        await Assert.That((await all.ToListAsync()).Count).IsEqualTo(3);
    }

    [Test]
    public async Task Query_model_rejects_invalid_selectors_and_tokens()
    {
        using var host = BuildHost();
        var repository = host.Services.GetRequiredService<IRepository<AzureTableRecord>>();

        await Assert.That(() => repository.Query().Where(entity => entity.Name.ToLower()).Is("ada"))
            .Throws<QueryNotSupportedException>();
        await Assert.That(() => repository.Query().Take(-1))
            .Throws<ArgumentOutOfRangeException>();

        var query = repository.Query().Where(entity => entity.Count).Is(1).Take(1);
        await Assert.That(async () => await query.WithContinuation("invalid").ToPageAsync())
            .Throws<QueryNotSupportedException>();
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(storage => storage.AddInMemoryProvider());
        return builder.Build();
    }
}
