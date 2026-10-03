using Azure.Data.Tables;
using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableFilterTranslatorTests
{
    [Test]
    public async Task String_quote_matches_odata_formatter()
    {
        var expected = TableClient.CreateQueryFilter($"Name eq {"O'Brien"}");
        var actual = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name == "O'Brien");
        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(actual).Contains("O''Brien");
    }

    [Test]
    public async Task Captured_string_is_escaped()
    {
        var name = "O'Brien";
        var actual = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name == name);
        await Assert.That(actual).IsEqualTo(TableClient.CreateQueryFilter($"Name eq {name}"));
    }

    [Test]
    public async Task Id_compares_row_key_as_string()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var actual = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Id == id);
        await Assert.That(actual).IsEqualTo(TableClient.CreateQueryFilter($"RowKey eq {id.ToString("D")}"));
        await Assert.That(actual).DoesNotContain("guid'");
    }

    [Test]
    public async Task Guid_property_uses_edm_guid()
    {
        var token = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var actual = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Token == token);
        await Assert.That(actual).IsEqualTo(TableClient.CreateQueryFilter($"Token eq {token}"));
        await Assert.That(actual).Contains("guid'");
    }

    [Test]
    public async Task Bool_number_and_flipped_comparison_use_odata_operators()
    {
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Active)).IsEqualTo("Active eq true");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => !e.Active)).IsEqualTo("not (Active eq true)");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Active == false)).IsEqualTo("Active eq false");

        var greater = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Count > 2);
        var flipped = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => 2 < e.Count);
        await Assert.That(greater).IsEqualTo(TableClient.CreateQueryFilter($"Count gt {2}"));
        await Assert.That(flipped).IsEqualTo(greater);
    }

    [Test]
    public async Task And_or_and_enum_are_parenthesized()
    {
        var combined = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name == "a" && e.Active);
        await Assert.That(combined).IsEqualTo("(Name eq 'a') and (Active eq true)");

        var either = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name == "a" || e.Name == "b");
        await Assert.That(either).Contains(" or ");

        var status = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Status == AzureTableStatus.Ready);
        await Assert.That(status).IsEqualTo(TableClient.CreateQueryFilter($"Status eq {"Ready"}"));
    }

    [Test]
    public async Task Partition_is_always_applied()
    {
        var filter = AzureTableFilterTranslator.WithPartition<AzureTableRecord>(e => e.Name == "a");
        await Assert.That(filter).StartsWith("(PartitionKey eq 'row') and (");
    }

    [Test]
    public async Task Equals_method_is_equality()
    {
        var actual = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name.Equals("Ada"));
        await Assert.That(actual).IsEqualTo(TableClient.CreateQueryFilter($"Name eq {"Ada"}"));
    }

    [Test]
    public async Task Substring_and_null_are_rejected()
    {
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name.Contains("a")))
            .Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name.StartsWith("a")))
            .Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Note == null))
            .Throws<NotSupportedException>();
    }
}
