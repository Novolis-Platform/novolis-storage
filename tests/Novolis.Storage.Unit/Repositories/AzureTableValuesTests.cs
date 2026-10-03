using System.Linq.Expressions;
using System.Reflection;
using Novolis.Storage.AzureTables;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableValuesTests
{
    [Test]
    public async Task Normalize_and_read_cover_supported_scalars()
    {
        var unspecified = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Unspecified);
        var utc = DateTime.SpecifyKind(unspecified, DateTimeKind.Utc);
        var local = DateTime.SpecifyKind(unspecified, DateTimeKind.Local);
        var offset = new DateTimeOffset(2024, 2, 3, 4, 5, 6, TimeSpan.FromHours(2));

        await Assert.That(AzureTableValues.Normalize(typeof(string), "Ada")).IsEqualTo("Ada");
        await Assert.That(AzureTableValues.Normalize(typeof(bool), true)).IsEqualTo(true);
        await Assert.That(AzureTableValues.Normalize(typeof(bool?), false)).IsEqualTo(false);
        await Assert.That(AzureTableValues.Normalize(typeof(byte), (byte)2)).IsEqualTo(2);
        await Assert.That(AzureTableValues.Normalize(typeof(sbyte), (sbyte)-3)).IsEqualTo(-3);
        await Assert.That(AzureTableValues.Normalize(typeof(short), (short)4)).IsEqualTo(4);
        await Assert.That(AzureTableValues.Normalize(typeof(ushort), (ushort)5)).IsEqualTo(5);
        await Assert.That(AzureTableValues.Normalize(typeof(int?), 6)).IsEqualTo(6);
        await Assert.That(AzureTableValues.Normalize(typeof(uint), 7u)).IsEqualTo(7L);
        await Assert.That(AzureTableValues.Normalize(typeof(long), 8L)).IsEqualTo(8L);
        await Assert.That(AzureTableValues.Normalize(typeof(float), 1.5f)).IsEqualTo(1.5d);
        await Assert.That(AzureTableValues.Normalize(typeof(double), 2.5d)).IsEqualTo(2.5d);
        var token = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await Assert.That(AzureTableValues.Normalize(typeof(Guid), token)).IsEqualTo(token);
        await Assert.That(AzureTableValues.Normalize(typeof(DateTime), utc)).IsEqualTo(new DateTimeOffset(utc));
        await Assert.That(AzureTableValues.Normalize(typeof(DateTime), unspecified)).IsEqualTo(new DateTimeOffset(utc));
        await Assert.That(((DateTimeOffset)AzureTableValues.Normalize(typeof(DateTime), local)).UtcDateTime).IsEqualTo(local.ToUniversalTime());
        await Assert.That(AzureTableValues.Normalize(typeof(DateTimeOffset), offset)).IsEqualTo(offset.ToUniversalTime());
        await Assert.That(AzureTableValues.Normalize(typeof(AzureTableStatus), AzureTableStatus.Ready)).IsEqualTo(nameof(AzureTableStatus.Ready));
        await Assert.That(AzureTableValues.Normalize(typeof(AzureTableStatus), (AzureTableStatus)99)).IsEqualTo("99");

        await Assert.That(AzureTableValues.Read(typeof(string), "Ada")).IsEqualTo("Ada");
        await Assert.That(AzureTableValues.Read(typeof(string), null!)).IsEqualTo(string.Empty);
        await Assert.That(AzureTableValues.Read(typeof(bool), "true")).IsEqualTo(true);
        await Assert.That(AzureTableValues.Read(typeof(byte), "2")).IsEqualTo((byte)2);
        await Assert.That(AzureTableValues.Read(typeof(sbyte), -3)).IsEqualTo((sbyte)-3);
        await Assert.That(AzureTableValues.Read(typeof(short), 4)).IsEqualTo((short)4);
        await Assert.That(AzureTableValues.Read(typeof(ushort), 5)).IsEqualTo((ushort)5);
        await Assert.That(AzureTableValues.Read(typeof(int?), "6")).IsEqualTo(6);
        await Assert.That(AzureTableValues.Read(typeof(uint), 7L)).IsEqualTo(7u);
        await Assert.That(AzureTableValues.Read(typeof(long), 8)).IsEqualTo(8L);
        await Assert.That(AzureTableValues.Read(typeof(double), 2)).IsEqualTo(2d);
        await Assert.That(AzureTableValues.Read(typeof(float), 1.5d)).IsEqualTo(1.5f);
        await Assert.That(AzureTableValues.Read(typeof(Guid), token)).IsEqualTo(token);
        await Assert.That(AzureTableValues.Read(typeof(Guid), token.ToString("D"))).IsEqualTo(token);
        await Assert.That(AzureTableValues.Read(typeof(DateTimeOffset), offset)).IsEqualTo(offset);
        await Assert.That(AzureTableValues.Read(typeof(DateTimeOffset), utc)).IsEqualTo(new DateTimeOffset(utc));
        await Assert.That(AzureTableValues.Read(typeof(DateTimeOffset), "2024-02-03T04:05:06.0000000Z")).IsEqualTo(new DateTimeOffset(utc));
        await Assert.That(AzureTableValues.Read(typeof(DateTime), new DateTimeOffset(utc))).IsEqualTo(utc);
        await Assert.That(AzureTableValues.Read(typeof(AzureTableStatus), nameof(AzureTableStatus.Draft))).IsEqualTo(AzureTableStatus.Draft);
    }

    [Test]
    public async Task Unsupported_values_and_properties_are_rejected()
    {
        await Assert.That(() => AzureTableValues.Normalize(typeof(decimal), 1m)).Throws<InvalidOperationException>();
        await Assert.That(() => AzureTableValues.Normalize(typeof(string), 1)).Throws<InvalidOperationException>();
        await Assert.That(() => AzureTableValues.Read(typeof(decimal), 1m)).Throws<InvalidOperationException>();

        var flags = typeof(AzureTableScalars).GetProperties();
        await Assert.That(AzureTableValues.IsStoredProperty(flags.Single(property => property.Name == nameof(AzureTableScalars.Text)))).IsTrue();
        await Assert.That(AzureTableValues.IsStoredProperty(flags.Single(property => property.Name == nameof(AzureTableScalars.Id)))).IsFalse();
        await Assert.That(AzureTableValues.IsStoredProperty(flags.Single(property => property.Name == nameof(AzureTableScalars.ReadOnly)))).IsFalse();
        await Assert.That(AzureTableValues.IsStoredProperty(flags.Single(property => property.GetIndexParameters().Length > 0))).IsFalse();
        await Assert.That(() => AzureTableValues.IsStoredProperty(typeof(AzureTableOdd).GetProperty(nameof(AzureTableOdd.Timestamp))!))
            .Throws<NotSupportedException>();
    }
}
