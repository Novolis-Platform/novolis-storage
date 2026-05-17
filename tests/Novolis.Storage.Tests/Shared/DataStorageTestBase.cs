using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;
using Novolis.Storage.Sqlite;
using TUnit.Core;

namespace Novolis.Storage.Tests.Shared;

public enum StorageType { Json, Sqlite }

public abstract class DataStorageTestBase<T> where T : class, IKeyed, new()
{
    private IHost? _host;

    public abstract StorageType GetStorageType();

    protected TRepository GetRepository<TRepository>() where TRepository : notnull
        => _host!.Services.GetRequiredService<TRepository>();

    public async Task SetUpHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:SqliteConnection"] =
            $"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db")}";
        builder.Configuration["ConnectionStrings:JsonConnection"] =
            Path.Combine(Path.GetTempPath(), "novolis-storage-tests", Guid.NewGuid().ToString());

        switch (GetStorageType())
        {
            case StorageType.Json:
                builder.Services.AddJsonDataStorage<T>(builder.Configuration);
                break;
            case StorageType.Sqlite:
                builder.Services.AddSqliteDataStorage<T>(builder.Configuration);
                break;
        }

        _host = builder.Build();
        await _host.StartAsync();
    }

    public async Task TearDownHost()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
    }
}
