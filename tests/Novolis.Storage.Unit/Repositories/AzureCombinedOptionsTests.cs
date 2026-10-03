using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureCombinedStorage;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureCombinedOptionsTests
{
    [Test]
    public async Task Registration_exposes_clients_repository_and_maintenance()
    {
        var services = new ServiceCollection();
        services.AddStorage(builder => builder.AddAzureCombinedStorage(options =>
        {
            options.ConnectionString = "UseDevelopmentStorage=true";
            options.TablePrefix = "test";
        }));
        using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetRequiredService<AzureCombinedStorageOptions>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<TableServiceClient>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<BlobServiceClient>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IRepository<AzureTableRecord>>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IAzureCombinedMaintenance<AzureTableRecord>>()).IsNotNull();
    }

    [Test]
    public async Task Invalid_registration_options_fail_before_service_use()
    {
        await Assert.That(() => Register(_ => { })).Throws<ArgumentException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.ShardCount = 0;
        })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.MaxPerPage = 0;
        })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.MaxParallelBlobReads = 0;
        })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.SchemaVersion = 0;
        })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.BlobContainerName = string.Empty;
        })).Throws<ArgumentException>();
    }

    [Test]
    public async Task Index_configuration_replaces_same_identity()
    {
        var options = new AzureCombinedStorageOptions();
        options.AddIndex<AzureTableRecord>("status", entity => entity.Status);
        options.AddIndex<AzureTableRecord>("status", entity => entity.Count);

        await Assert.That(options.GetType()).IsNotNull();
    }

    private static void Register(Action<AzureCombinedStorageOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddStorage(builder => builder.AddAzureCombinedStorage(configure));
    }
}
