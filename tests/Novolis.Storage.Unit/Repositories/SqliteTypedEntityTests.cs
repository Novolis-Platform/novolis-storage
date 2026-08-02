using Novolis.Storage.Abstractions;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class TypedSqliteEntity : IHasId
{
    public Guid Id { get; set; }
    public int IntVal { get; set; }
    public long LongVal { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Flag { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SqliteTypedEntityTests : SqliteDataStorageTestBase<TypedSqliteEntity>
{
    [Test]
    public async Task Upsert_get_roundtrips_scalar_types()
    {
        await SetUpHost();
        try
        {
            var repository = GetService<IRepository<TypedSqliteEntity>>();
            var entity = new TypedSqliteEntity
            {
                Id = Guid.NewGuid(),
                IntVal = 42,
                LongVal = 9_000_000_000,
                Text = "typed",
                CreatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc),
                Flag = true,
            };

            await repository.UpsertAsync(entity);
            var loaded = await repository.TryGetAsync(entity.Id);
            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.IntVal).IsEqualTo(42);
            await Assert.That(loaded.LongVal).IsEqualTo(9_000_000_000);
            await Assert.That(loaded.Text).IsEqualTo("typed");
            await Assert.That(loaded.Flag).IsTrue();
        }
        finally
        {
            await TearDownHost();
        }
    }
}
