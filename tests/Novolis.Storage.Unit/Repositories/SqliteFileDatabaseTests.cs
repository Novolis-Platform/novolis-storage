using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Sqlite;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class SqliteFileDatabaseTests
{
    [Test]
    public async Task File_database_creates_directory_and_roundtrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(dir, "nested", "store.db");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddSqliteProvider(o => o.ConnectionString = $"Data Source={dbPath}"));
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "file-db", Boolean = true };
            await repository.UpsertAsync(entity);
            await Assert.That(Directory.Exists(Path.GetDirectoryName(dbPath)!)).IsTrue();
            await Assert.That(await repository.TryGetAsync(entity.Id)).IsNotNull();
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
