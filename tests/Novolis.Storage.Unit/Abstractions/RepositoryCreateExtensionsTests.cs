using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Abstractions;

public sealed class RepositoryCreateExtensionsTests
{
    [Test]
    public async Task CreateAsync_generates_id_and_persists()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repo = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var idProvider = host.Services.GetRequiredService<IIdProvider>();
            var created = await repo.CreateAsync(idProvider, id => new ExampleClass { Id = id, SomeData = "created" });
            await Assert.That(created.Id).IsNotEqualTo(Guid.Empty);
            var loaded = await repo.TryGetAsync(created.Id);
            await Assert.That(loaded!.SomeData).IsEqualTo("created");
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
