using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableNamesTests
{
    [Test]
    public async Task Identity_is_alphanumeric_and_at_least_three_characters()
    {
        await Assert.That(AzureTableNames.FromIdentity("OkName")).IsEqualTo("OkName");
        await Assert.That(AzureTableNames.FromIdentity("A")).IsEqualTo("Axx");
        await Assert.That(AzureTableNames.FromIdentity("9ab")).IsEqualTo("T9ab");
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
