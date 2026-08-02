using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.InMemory;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class InMemoryRepositoryTests
{
    [Test]
    public async Task Upsert_get_delete_roundtrip()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddInMemoryProvider());
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "memory" };
            await repository.UpsertAsync(entity);
            await Assert.That((await repository.TryGetAsync(entity.Id))!.SomeData).IsEqualTo("memory");
            await Assert.That(await repository.DeleteAsync(entity.Id)).IsTrue();
            await Assert.That(await repository.TryGetAsync(entity.Id)).IsNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task All_returns_stored_entities()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddInMemoryProvider());
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var first = new ExampleClass { Id = Guid.NewGuid(), SomeData = "a" };
            var second = new ExampleClass { Id = Guid.NewGuid(), SomeData = "b" };
            await repository.UpsertAsync(first);
            await repository.UpsertAsync(second);
            await Assert.That(repository.All().Count()).IsEqualTo(2);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
