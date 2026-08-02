using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class JsonRepositoryErrorTests
{
    [Test]
    public async Task TryGet_missing_entity_returns_null()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await Assert.That(await repository.TryGetAsync(Guid.NewGuid())).IsNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task Delete_missing_entity_returns_false()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await Assert.That(await repository.DeleteAsync(Guid.NewGuid())).IsFalse();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task TryGet_ignores_corrupt_json_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var id = Guid.NewGuid();
            var typeDir = Path.Combine(root, nameof(ExampleClass));
            Directory.CreateDirectory(typeDir);
            var path = Path.Combine(typeDir, id.ToString("N") + ".json");
            await File.WriteAllTextAsync(path, "{not-json");
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await Assert.That(await repository.TryGetAsync(id)).IsNull();
            await Assert.That(repository.All().Count()).IsEqualTo(0);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task Upsert_then_delete_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "temp" };
            await repository.UpsertAsync(entity);
            await Assert.That(await repository.DeleteAsync(entity.Id)).IsTrue();
            await Assert.That(await repository.TryGetAsync(entity.Id)).IsNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
