using Novolis.Storage.Abstractions;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class SqliteRepositoryTests : SqliteDataStorageTestBase<ExampleClass>
{
    [Test]
    public async Task Upsert_get_delete_roundtrip()
    {
        await SetUpHost();
        try
        {
            var repository = GetService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "Sqlite", Boolean = true };

            await repository.UpsertAsync(entity);
            var loaded = await repository.TryGetAsync(entity.Id);
            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.SomeData).IsEqualTo("Sqlite");
            await Assert.That(loaded.Boolean).IsTrue();

            var deleted = await repository.DeleteAsync(entity.Id);
            await Assert.That(deleted).IsTrue();
            await Assert.That(await repository.TryGetAsync(entity.Id)).IsNull();
        }
        finally
        {
            await TearDownHost();
        }
    }

    [Test]
    public async Task All_returns_upserted_entities()
    {
        await SetUpHost();
        try
        {
            var repository = GetService<IRepository<ExampleClass>>();
            var first = new ExampleClass { Id = Guid.NewGuid(), SomeData = "one" };
            var second = new ExampleClass { Id = Guid.NewGuid(), SomeData = "two" };

            await repository.UpsertAsync(first);
            await repository.UpsertAsync(second);

            var all = repository.All().ToList();
            await Assert.That(all).Count().IsEqualTo(2);
            await Assert.That(all.Select(x => x.Id)).Contains(first.Id);
            await Assert.That(all.Select(x => x.Id)).Contains(second.Id);
        }
        finally
        {
            await TearDownHost();
        }
    }
}
