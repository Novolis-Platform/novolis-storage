using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using TUnit.Core;

namespace Novolis.Storage.Tests.Shared;

public abstract class DataStorageTestBase<T> where T : class, IHasId, new()
{
    private IHost? _host;

    protected TService GetService<TService>() where TService : notnull =>
        _host!.Services.GetRequiredService<TService>();

    public async Task SetUpHost()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString("N"));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddStorage(b => b.AddJsonProvider(o => o.RootPath = root));
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
