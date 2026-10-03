using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableEntityMapperTests
{
    [Test]
    public async Task Roundtrip_keeps_scalars_and_omits_nulls()
    {
        var when = new DateTimeOffset(2026, 4, 5, 6, 7, 8, 123, TimeSpan.Zero);
        var source = new AzureTableRecord
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "O'Brien",
            Note = null,
            Active = false,
            Count = 0,
            Token = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            When = when,
            Status = AzureTableStatus.Ready,
        };

        var row = AzureTableEntityMapper.ToTable(source);
        await Assert.That(row.PartitionKey).IsEqualTo("row");
        await Assert.That(row.RowKey).IsEqualTo(source.Id.ToString("D"));
        await Assert.That(row.ContainsKey(nameof(AzureTableRecord.Note))).IsFalse();

        var copy = AzureTableEntityMapper.ToObject<AzureTableRecord>(row);
        await Assert.That(copy.Id).IsEqualTo(source.Id);
        await Assert.That(copy.Name).IsEqualTo(source.Name);
        await Assert.That(copy.Note).IsNull();
        await Assert.That(copy.Active).IsFalse();
        await Assert.That(copy.Count).IsEqualTo(0);
        await Assert.That(copy.Token).IsEqualTo(source.Token);
        await Assert.That(copy.When).IsEqualTo(when);
        await Assert.That(copy.Status).IsEqualTo(AzureTableStatus.Ready);
    }
}
