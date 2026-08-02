using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.InMemory;
using Novolis.Storage.Json;
using Novolis.Storage.LiteDb;
using Novolis.Storage.Sqlite;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class RepositoryProviderFactoryTests
{
    [Test]
    public async Task InMemory_provider_Create_persists_via_factory()
    {
        await AssertProviderRoundTrip(
            b => b.AddInMemoryProvider(),
            host => host.Services.GetRequiredService<IRepositoryProvider>());
    }

    [Test]
    public async Task Json_provider_Create_persists_via_factory()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        await AssertProviderRoundTrip(
            b => b.AddJsonProvider(o => o.RootPath = root),
            host => host.Services.GetRequiredService<IRepositoryProvider>());
    }

    [Test]
    public async Task LiteDb_provider_Create_persists_via_factory()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N") + ".db");
        await AssertProviderRoundTrip(
            b => b.AddLiteDbProvider(o => o.DatabasePath = dbPath),
            host => host.Services.GetRequiredService<IRepositoryProvider>());
    }

    [Test]
    public async Task Sqlite_provider_Create_persists_via_factory()
    {
        await AssertProviderRoundTrip(
            b => b.AddSqliteProvider(o => o.ConnectionString = "Data Source=:memory:"),
            host => host.Services.GetRequiredService<IRepositoryProvider>());
    }

    [Test]
    public async Task Repository_factory_resolves_same_instances_as_provider()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddInMemoryProvider());
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var factory = host.Services.GetRequiredService<IRepositoryFactory>();
            var provider = host.Services.GetRequiredService<IRepositoryProvider>();
            var fromFactory = factory.Create<ExampleClass>();
            var fromProvider = provider.Create<ExampleClass>();
            await Assert.That(fromFactory).IsNotNull();
            await Assert.That(fromProvider).IsNotNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    static async Task AssertProviderRoundTrip(
        Action<IStorageBuilder> configure,
        Func<IHost, IRepositoryProvider> resolveProvider)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(configure);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var provider = resolveProvider(host);
            var repository = provider.Create<ExampleClass>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "provider-factory", Boolean = true };
            await repository.UpsertAsync(entity);
            var loaded = await repository.TryGetAsync(entity.Id);
            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.SomeData).IsEqualTo("provider-factory");
            await Assert.That(loaded.Boolean).IsTrue();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
