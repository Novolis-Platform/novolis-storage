using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.IO.Workspace.Testing;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using Novolis.Storage.LiteDb;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class JsonRepositoryCoverageTests
{
    [Test]
    public async Task All_yields_nothing_when_type_directory_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await Assert.That(repository.All().Count()).IsEqualTo(0);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task Upsert_rejects_empty_id()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await Assert.That(async () => await repository.UpsertAsync(new ExampleClass { Id = Guid.Empty, SomeData = "x" }))
                .Throws<ArgumentException>();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task Read_only_provider_rejects_writes()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o =>
        {
            o.RootPath = root;
            o.ReadOnly = true;
        }));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "ro" };
            await Assert.That(async () => await repository.UpsertAsync(entity))
                .Throws<InvalidOperationException>();
            await Assert.That(async () => await repository.DeleteAsync(entity.Id))
                .Throws<InvalidOperationException>();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task LiteDb_provider_builds_connection_string_with_filename_only()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N") + ".db");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddLiteDbProvider(o => o.DatabasePath = dbPath));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await repository.UpsertAsync(new ExampleClass { Id = Guid.NewGuid(), SomeData = "conn" });
            await Assert.That(repository.All().Count()).IsEqualTo(1);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
