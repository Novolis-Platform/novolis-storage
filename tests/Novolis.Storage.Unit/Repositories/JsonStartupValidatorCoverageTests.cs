using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.IO.Workspace;
using Novolis.IO.Workspace.Testing;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class JsonStartupValidatorCoverageTests
{
    [Test]
    public async Task Validate_rejects_blank_root_before_workspace_access()
    {
        using var workspace = new InMemoryFileWorkspace(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        await Assert.That(() => JsonFilesStartupValidator.Validate(new JsonFilesOptions { RootPath = string.Empty }, workspace, out _))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Validate_throws_when_read_only_and_root_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"), "missing");
        using var workspace = new PhysicalFileWorkspace(Path.GetDirectoryName(root)!);
        var options = new JsonFilesOptions { RootPath = root, ReadOnly = true, CreateIfMissing = false };
        await Assert.That(() => JsonFilesStartupValidator.Validate(options, workspace, out _))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Validate_creates_root_when_allowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"), "new-root");
        using var workspace = new PhysicalFileWorkspace(Path.GetDirectoryName(root)!);
        var options = new JsonFilesOptions { RootPath = root, CreateIfMissing = true };
        JsonFilesStartupValidator.Validate(options, workspace, out var processLock);
        await Assert.That(workspace.DirectoryExists(root)).IsTrue();
        processLock?.Dispose();
    }

    [Test]
    public async Task Json_provider_process_lock_is_disposed_on_shutdown()
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
            await repository.UpsertAsync(new ExampleClass { Id = Guid.NewGuid(), SomeData = "lock-dispose" });
            var processLock = host.Services.GetServices<IDisposable>().LastOrDefault();
            processLock?.Dispose();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task TryGetAsync_deserializes_via_read_stream()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "stream-read" };
            await repository.UpsertAsync(entity);
            var loaded = await repository.TryGetAsync(entity.Id);
            await Assert.That(loaded?.SomeData).IsEqualTo("stream-read");
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [Test]
    public async Task TryGetAsync_returns_null_for_invalid_json_on_disk()
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
            await File.WriteAllTextAsync(Path.Combine(typeDir, id.ToString("N") + ".json"), "{bad");
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            await Assert.That(await repository.TryGetAsync(id)).IsNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
