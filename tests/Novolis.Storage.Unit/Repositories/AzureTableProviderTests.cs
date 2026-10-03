using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureTables;
using Novolis.Storage.InMemory;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableProviderTests
{
    [Test]
    public async Task Missing_connection_string_is_rejected()
    {
        var services = new ServiceCollection();
        await Assert.That(() => services.AddStorage(b => b.AddAzureTableProvider(_ => { })))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Page_size_outside_service_limits_is_rejected()
    {
        await Assert.That(() => Register(o => o.MaxPerPage = 0)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Register(o => o.MaxPerPage = 1001)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Provider_resolves_clients_and_repositories()
    {
        var services = new ServiceCollection();
        services.AddStorage(b => b.AddAzureTableProvider(o =>
        {
            o.ConnectionString = "UseDevelopmentStorage=true";
            o.MaxPerPage = 1000;
        }));
        await using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<TableServiceClient>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IRepositoryProvider>().Create<AzureTableRecord>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IRepository<AzureTableRecord>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IAzureTableRepository<AzureTableRecord>>()).IsNotNull();
    }

    [Test]
    public async Task Configure_null_is_rejected()
    {
        var services = new ServiceCollection();
        await Assert.That(() => services.AddStorage(b => b.AddAzureTableProvider(null!))).Throws<ArgumentNullException>();
    }

    private static void Register(Action<AzureTableOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddStorage(b => b.AddAzureTableProvider(o =>
        {
            o.ConnectionString = "UseDevelopmentStorage=true";
            configure(o);
        }));
    }

    [Test]
    public async Task Null_query_is_rejected_before_the_service()
    {
        var options = new AzureTableOptions { ConnectionString = "UseDevelopmentStorage=true" };
        var repo = new AzureTableRepository<AzureTableRecord>(new TableServiceClient(options.ConnectionString), options);
        await Assert.That(() => repo.QueryAsync(null!)).Throws<ArgumentNullException>();
    }
}
