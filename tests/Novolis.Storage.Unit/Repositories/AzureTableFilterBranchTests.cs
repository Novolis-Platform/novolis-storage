using System.Linq.Expressions;
using Azure.Data.Tables;
using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableFilterBranchTests
{
    [Test]
    public async Task Comparison_operators_cover_both_sides()
    {
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Count == 1)).Contains(" eq ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => 1 == e.Count)).Contains(" eq ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Count != 1)).Contains(" ne ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => 1 != e.Count)).Contains(" ne ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Count > 1)).Contains(" gt ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => 1 > e.Count)).Contains(" lt ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Count >= 1)).Contains(" ge ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => 1 >= e.Count)).Contains(" le ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Count < 1)).Contains(" lt ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => 1 < e.Count)).Contains(" gt ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Count <= 1)).Contains(" le ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => 1 <= e.Count)).Contains(" ge ");
        await Assert.That(() => AzureTableFilterTranslator.Operator(ExpressionType.Add, false)).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Logical_operators_static_equals_and_dates_translate()
    {
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Active & e.Count > 0)).Contains(" and ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Active | e.Count > 0)).Contains(" or ");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => string.Equals(e.Name, "Ada"))).Contains("Ada");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => string.Equals("Ada", e.Name))).Contains("Ada");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name == string.Empty)).Contains("''");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableScalars>(e => e.Optional == false)).Contains("false");
        await Assert.That(AzureTableFilterTranslator.Translate<AzureTableScalars>(e => e.Optional)).Contains("eq true");

        var offset = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
        var actual = AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.When == offset);
        await Assert.That(actual).IsEqualTo(TableClient.CreateQueryFilter($"When eq {offset.ToUniversalTime()}"));
    }

    [Test]
    public async Task Unsupported_shapes_are_rejected()
    {
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name == e.Note)).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => true == false)).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => string.Equals("a", "b"))).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => "Ada".Equals(e.Name))).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name.Equals("Ada", StringComparison.Ordinal))).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Active ? e.Count == 1 : e.Count == 2)).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableOdd>(e => e.flagField)).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableOdd>(e => e.flagField == true)).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableOdd>(e => e.Timestamp == "x")).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableFilterTranslator.Translate<AzureTableRecord>(e => e.Name)).Throws<NotSupportedException>();

        var parameter = Expression.Parameter(typeof(AzureTableRecord), "e");
        var equals = typeof(object).GetMethod(nameof(object.Equals), [typeof(object), typeof(object)])!;
        var body = Expression.Equal(
            Expression.Convert(Expression.Property(parameter, nameof(AzureTableRecord.Id)), typeof(object)),
            Expression.Convert(Expression.Constant("abc"), typeof(object)),
            false,
            equals);
        var lambda = Expression.Lambda<Func<AzureTableRecord, bool>>(body, parameter);
        await Assert.That(AzureTableFilterTranslator.Translate(lambda)).Contains("abc");

        var count = Expression.Property(parameter, nameof(AzureTableRecord.Count));
        var checkedBody = Expression.GreaterThan(
            Expression.ConvertChecked(count, typeof(long)),
            Expression.Constant(1L));
        var checkedLambda = Expression.Lambda<Func<AzureTableRecord, bool>>(checkedBody, parameter);
        await Assert.That(AzureTableFilterTranslator.Translate(checkedLambda)).Contains("gt");
    }
}
