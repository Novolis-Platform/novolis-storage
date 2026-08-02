using System.Globalization;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class SqliteTypeMatrixEntity : IHasId
{
    public Guid Id { get; set; }
    public int IntVal { get; set; }
    public long LongVal { get; set; }
    public short ShortVal { get; set; }
    public byte ByteVal { get; set; }
    public uint UIntVal { get; set; }
    public ulong ULongVal { get; set; }
    public ushort UShortVal { get; set; }
    public sbyte SByteVal { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid? OptionalGuid { get; set; }
    public bool Flag { get; set; }
    public bool? OptionalFlag { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? OptionalDate { get; set; }
    public decimal Amount { get; set; }
    public decimal? OptionalAmount { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan? OptionalDuration { get; set; }
    public DateTimeOffset Offset { get; set; }
    public DateTimeOffset? OptionalOffset { get; set; }
}

public sealed class SqliteTypeMatrixEntityTests : SqliteDataStorageTestBase<SqliteTypeMatrixEntity>
{
    [Test]
    public async Task Upsert_get_roundtrips_full_type_matrix()
    {
        await SetUpHost();
        try
        {
            var repository = GetService<IRepository<SqliteTypeMatrixEntity>>();
            var offset = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.FromHours(2));
            var entity = new SqliteTypeMatrixEntity
            {
                Id = Guid.NewGuid(),
                IntVal = 42,
                LongVal = 9_000_000_000,
                ShortVal = 9,
                ByteVal = 5,
                UIntVal = 11,
                ULongVal = 22,
                UShortVal = 13,
                SByteVal = -4,
                Text = "matrix",
                OptionalGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Flag = true,
                OptionalFlag = false,
                CreatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc),
                OptionalDate = new DateTime(2024, 7, 2, 8, 30, 0, DateTimeKind.Utc),
                Amount = 12345,
                OptionalAmount = 67890,
                Duration = new TimeSpan(1, 2, 3),
                OptionalDuration = new TimeSpan(4, 5, 6),
                Offset = offset,
                OptionalOffset = offset.AddHours(1),
            };

            await repository.UpsertAsync(entity);
            var loaded = await repository.TryGetAsync(entity.Id);
            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.IntVal).IsEqualTo(42);
            await Assert.That(loaded.LongVal).IsEqualTo(9_000_000_000);
            await Assert.That(loaded.ShortVal).IsEqualTo((short)9);
            await Assert.That(loaded.ByteVal).IsEqualTo((byte)5);
            await Assert.That(loaded.UIntVal).IsEqualTo(11U);
            await Assert.That(loaded.ULongVal).IsEqualTo(22UL);
            await Assert.That(loaded.UShortVal).IsEqualTo((ushort)13);
            await Assert.That(loaded.SByteVal).IsEqualTo((sbyte)-4);
            await Assert.That(loaded.Text).IsEqualTo("matrix");
            await Assert.That(loaded.OptionalGuid).IsEqualTo(entity.OptionalGuid);
            await Assert.That(loaded.Flag).IsTrue();
            await Assert.That(loaded.OptionalFlag).IsFalse();
            await Assert.That(loaded.Amount).IsEqualTo(12345);
            await Assert.That(loaded.OptionalAmount).IsEqualTo(67890);
            await Assert.That(loaded.Duration).IsEqualTo(new TimeSpan(1, 2, 3));
            await Assert.That(loaded.OptionalDuration).IsEqualTo(new TimeSpan(4, 5, 6));
            await Assert.That(loaded.Offset.ToString("o", CultureInfo.InvariantCulture))
                .IsEqualTo(offset.ToString("o", CultureInfo.InvariantCulture));

            var all = repository.All().ToList();
            await Assert.That(all.Count).IsEqualTo(1);
        }
        finally
        {
            await TearDownHost();
        }
    }
}
