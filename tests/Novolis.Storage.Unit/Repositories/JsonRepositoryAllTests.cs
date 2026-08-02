using Novolis.Storage.Abstractions;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class JsonRepositoryAllTests : DataStorageTestBase<ExampleClass>
{
    [Test]
    public async Task All_lists_persisted_entities()
    {
        await SetUpHost();
        try
        {
            var repository = GetService<IRepository<ExampleClass>>();
            var first = new ExampleClass { Id = Guid.NewGuid(), SomeData = "one" };
            var second = new ExampleClass { Id = Guid.NewGuid(), SomeData = "two" };
            await repository.UpsertAsync(first);
            await repository.UpsertAsync(second);
            await Assert.That(repository.All().Count()).IsEqualTo(2);
        }
        finally
        {
            await TearDownHost();
        }
    }
}
