using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.LiteDb;

namespace Novolis.Storage.Tests.Shared;

public abstract class LiteDbDataStorageTestBase<T> where T : class, IHasId, new()
{
    private IHost? _host;

    protected TService GetService<TService>() where TService : notnull =>
        _host!.Services.GetRequiredService<TService>();

    public async Task SetUpHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddLiteDbProvider(o => o.DatabasePath = ":memory:"));
        _host = builder.Build();
        await _host.StartAsync();
    }

    public async Task TearDownHost()
    {
        if (_host != null)
            await _host.StopAsync();
        _host?.Dispose();
    }
}
