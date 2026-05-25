using Novolis.Storage.Abstractions;
using Novolis.Storage.Tests.Shared;
using TUnit.Core;

namespace Novolis.Storage.Tests.Repositories;

public sealed class JsonRepositoryTests : DataStorageTestBase<ExampleClass>
{
    [Test]
    public async Task Upsert_get_delete_roundtrip()
    {
        await SetUpHost();
        try
        {
            var repository = GetService<IRepository<ExampleClass>>();
            var testData1 = new ExampleClass { Id = Guid.NewGuid(), SomeData = "Test1", Boolean = true };
            var testData2 = new ExampleClass { Id = Guid.NewGuid(), SomeData = "Test2" };

            await repository.UpsertAsync(testData1);
            await repository.UpsertAsync(testData2);

            var item1 = await repository.TryGetAsync(testData1.Id);
            await Assert.That(item1).IsNotNull();
            await Assert.That(item1!.SomeData).IsEqualTo("Test1");

            var deleted = await repository.DeleteAsync(testData1.Id);
            await Assert.That(deleted).IsTrue();
            await Assert.That(await repository.TryGetAsync(testData1.Id)).IsNull();
        }
        finally
        {
            await TearDownHost();
        }
    }
}
