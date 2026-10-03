using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableNamesTests
{
    [Test]
    public async Task Identity_is_alphanumeric_and_at_least_three_characters()
    {
        await Assert.That(AzureTableNames.FromIdentity("OkName")).IsEqualTo("OkName");
        await Assert.That(AzureTableNames.FromIdentity("A")).IsEqualTo("Axx");
        await Assert.That(AzureTableNames.FromIdentity("ab")).IsEqualTo("abx");
        await Assert.That(AzureTableNames.FromIdentity("")).IsEqualTo("Txx");
        await Assert.That(AzureTableNames.FromIdentity("1")).IsEqualTo("T1x");
        await Assert.That(AzureTableNames.FromIdentity("9ab")).IsEqualTo("T9ab");
        await Assert.That(AzureTableNames.ForType(typeof(AzureTableRecord), null)).Contains(nameof(AzureTableRecord));
        await Assert.That(AzureTableNames.ForType(typeof(AzureTableRecord), "  ")).Contains(nameof(AzureTableRecord));
        await Assert.That(AzureTableNames.ForType(typeof(AzureTableRecord), "pre")).StartsWith("pre");
    }

    [Test]
    public async Task Long_names_stay_within_table_limits_and_keep_the_prefix()
    {
        var a = AzureTableNames.ForType(typeof(AzureTableRecord), new string('a', 40));
        var b = AzureTableNames.ForType(typeof(AzureTableRecord), new string('b', 40));
        await Assert.That(a.Length).IsEqualTo(63);
        await Assert.That(b.Length).IsEqualTo(63);
        await Assert.That(a).IsNotEqualTo(b);
    }
}
