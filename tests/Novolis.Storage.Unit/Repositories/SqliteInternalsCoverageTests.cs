using System.Reflection;
using Novolis.Storage.Sqlite.Internals;

namespace Novolis.Storage.Tests.Repositories;

public sealed class SqliteInternalsCoverageTests
{
    [Test]
    public async Task Type_mapping_covers_sqlite_affinites()
    {
        var mapping = new SqliteTypeMappingDefinition();
        await Assert.That(mapping[typeof(int)]).IsEqualTo("INTEGER");
        await Assert.That(mapping[typeof(string)]).IsEqualTo("TEXT");
        await Assert.That(mapping[typeof(decimal)]).IsEqualTo("NUMERIC");
        await Assert.That(mapping[typeof(double)]).IsEqualTo("REAL");
        await Assert.That(mapping[typeof(byte[])]).IsEqualTo("BLOB");

        var integerTypes = new IntegerTypes().ToList();
        await Assert.That(integerTypes).Contains(typeof(int));
        await Assert.That(new NumericTypes().ToList()).Contains(typeof(decimal));
        await Assert.That(new RealTypes().ToList()).Contains(typeof(double));
        await Assert.That(new TextTypes().ToList()).Contains(typeof(Guid));
        await Assert.That(new BlobTypes().ToList()).Contains(typeof(byte[]));
    }

    [Test]
    public async Task Type_mapping_unknown_type_throws()
    {
        var mapping = new SqliteTypeMappingDefinition();
        await Assert.That(() => mapping[typeof(object)]).Throws<KeyNotFoundException>();
        await Assert.That(() => mapping["UNKNOWN"]).Throws<KeyNotFoundException>();
    }
}
