using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.LiteDb;
using Novolis.Storage.Tests.Shared;

namespace Novolis.Storage.Tests.Repositories;

public sealed class LiteDbProviderOptionsTests
{
    [Test]
    public async Task LiteDb_provider_accepts_password_and_shared_file_path()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N") + ".db");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddLiteDbProvider(o =>
        {
            o.DatabasePath = dbPath;
            o.Password = "test-password";
        }));

        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var repository = host.Services.GetRequiredService<IRepository<ExampleClass>>();
            var entity = new ExampleClass { Id = Guid.NewGuid(), SomeData = "litedb" };
            await repository.UpsertAsync(entity);
            await Assert.That(await repository.TryGetAsync(entity.Id)).IsNotNull();
            await Assert.That(host.Services.GetRequiredService<LiteDbOptions>().Password).IsEqualTo("test-password");
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
