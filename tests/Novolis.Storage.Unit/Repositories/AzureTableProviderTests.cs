using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureTables;

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
        var services = new ServiceCollection();
        await Assert.That(() => services.AddStorage(b => b.AddAzureTableProvider(o =>
        {
            o.ConnectionString = "UseDevelopmentStorage=true";
            o.MaxPerPage = 0;
        }))).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Null_query_is_rejected_before_the_service()
    {
        var options = new AzureTableOptions { ConnectionString = "UseDevelopmentStorage=true" };
        var repo = new AzureTableRepository<AzureTableRecord>(new TableServiceClient(options.ConnectionString), options);
        await Assert.That(() => repo.QueryAsync(null!)).Throws<ArgumentNullException>();
    }
}
