using Azure.Data.Tables;
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

    [Test]
    public async Task Scalar_matrix_roundtrips_through_a_table_entity()
    {
        var when = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var source = new AzureTableScalars
        {
            Id = Guid.NewGuid(),
            Text = "n",
            Flag = true,
            Optional = false,
            B = 1,
            Sb = -2,
            S = 3,
            Us = 4,
            I = 5,
            Score = 6,
            Ui = 7,
            L = 8,
            F = 1.25f,
            D = 2.5,
            Token = Guid.NewGuid(),
            When = when,
            WhenOffset = new DateTimeOffset(when),
            Status = AzureTableStatus.Draft,
        };

        var copy = AzureTableEntityMapper.ToObject<AzureTableScalars>(AzureTableEntityMapper.ToTable(source));
        await Assert.That(copy.Text).IsEqualTo(source.Text);
        await Assert.That(copy.Flag).IsTrue();
        await Assert.That(copy.Optional).IsFalse();
        await Assert.That(copy.B).IsEqualTo(source.B);
        await Assert.That(copy.Sb).IsEqualTo(source.Sb);
        await Assert.That(copy.S).IsEqualTo(source.S);
        await Assert.That(copy.Us).IsEqualTo(source.Us);
        await Assert.That(copy.I).IsEqualTo(source.I);
        await Assert.That(copy.Score).IsEqualTo(source.Score);
        await Assert.That(copy.Ui).IsEqualTo(source.Ui);
        await Assert.That(copy.L).IsEqualTo(source.L);
        await Assert.That(copy.F).IsEqualTo(source.F);
        await Assert.That(copy.D).IsEqualTo(source.D);
        await Assert.That(copy.Token).IsEqualTo(source.Token);
        await Assert.That(copy.When).IsEqualTo(when);
        await Assert.That(copy.WhenOffset).IsEqualTo(source.WhenOffset);
        await Assert.That(copy.Status).IsEqualTo(AzureTableStatus.Draft);
    }

    [Test]
    public async Task Null_cells_and_unwritable_ids_are_rejected_or_skipped()
    {
        await Assert.That(() => AzureTableEntityMapper.ToTable<AzureTableRecord>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => AzureTableEntityMapper.ToTable(new AzureTableOdd())).Throws<NotSupportedException>();

        var row = new TableEntity("row", Guid.NewGuid().ToString("D"));
        row[nameof(AzureTableRecord.Name)] = null;
        var copy = AzureTableEntityMapper.ToObject<AzureTableRecord>(row);
        await Assert.That(copy.Name).IsEqualTo(string.Empty);

        var frozen = new TableEntity("row", Guid.NewGuid().ToString("D"));
        await Assert.That(() => AzureTableEntityMapper.ToObject<AzureTableFrozenId>(frozen)).Throws<InvalidOperationException>();
        await Assert.That(() => AzureTableEntityMapper.ToObject<AzureTableExplicitId>(frozen)).Throws<InvalidOperationException>();
    }
}
