using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class JsonProviderRegistrationTests
{
    [Test]
    public async Task Json_provider_with_process_lock_starts()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o =>
        {
            o.RootPath = root;
            o.UseProcessLock = true;
        }));

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "json-lock" };
            await repository.UpsertAsync(entity);
            await Assert.That(await repository.TryGetAsync(entity.Id)).IsNotNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task Json_provider_releases_its_process_lock_when_the_host_stops()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));

        await StartAndStopLockedHostAsync(root);
        await StartAndStopLockedHostAsync(root);
    }

    [Test]
    public async Task Json_provider_defaults_temp_root_when_unset()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(_ => { }));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await Assert.That(repository).IsNotNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static async Task StartAndStopLockedHostAsync(string root)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(options =>
        {
            options.RootPath = root;
            options.UseProcessLock = true;
        }));

        using var host = builder.Build();
        await host.StartAsync();
        _ = host.Services.GetRequiredService<IRepository<ExampleClass>>();
        await host.StopAsync();
    }
}
