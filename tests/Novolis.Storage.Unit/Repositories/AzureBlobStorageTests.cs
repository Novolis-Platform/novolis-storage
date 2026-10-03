using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureBlob;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureBlobStorageTests
{
    [Test]
    public async Task Named_blobs_round_trip_list_and_delete()
    {
        var handler = new AzureTableMemoryHandler { DelayRequests = true };
        var container = CreateContainer(handler, out _);

        await Task.WhenAll(
            container.TryGetAsync("concurrent-a.json").AsTask(),
            container.TryGetAsync("concurrent-b.json").AsTask());
        await Assert.That(await container.TryGetAsync("missing.json")).IsNull();
        await Assert.That(await container.DeleteAsync("missing.json")).IsFalse();

        await container.UpsertAsync(new AzureBlobRecord
        {
            Name = "first.json",
            Value = "one",
        });
        await container.UpsertAsync(new AzureBlobRecord
        {
            Name = "second.json",
            Value = "two",
        });
        await container.UpsertAsync(new AzureBlobRecord
        {
            Name = "first.json",
            Value = "updated",
        });

        await Assert.That((await container.TryGetAsync("first.json"))!.Value).IsEqualTo("updated");
        var values = new List<AzureBlobRecord>();
        await foreach (var value in container.ListAsync())
            values.Add(value);

        await Assert.That(values.Select(value => value.Name).ToArray())
            .IsEquivalentTo(new[] { "first.json", "second.json" });
        await Assert.That(await container.DeleteAsync("first.json")).IsTrue();
        await Assert.That(await container.DeleteAsync("first.json")).IsFalse();
        await Assert.That(await container.TryGetAsync("first.json")).IsNull();
    }

    [Test]
    public async Task Reads_reject_null_and_mismatched_payloads()
    {
        var handler = new AzureTableMemoryHandler();
        var container = CreateContainer(handler, out var service);
        var client = service.GetBlobContainerClient(ContainerName);
        await Assert.That(await container.TryGetAsync("not-present.json")).IsNull();

        await client.GetBlobClient("null.json").UploadAsync(
            new BinaryData("null"),
            overwrite: true);
        await Assert.That(async () => await container.TryGetAsync("null.json"))
            .Throws<InvalidDataException>();

        await client.GetBlobClient("mismatch.json").UploadAsync(
            new BinaryData("{\"name\":\"other.json\",\"value\":\"bad\"}"),
            overwrite: true);
        await Assert.That(async () => await container.TryGetAsync("mismatch.json"))
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Invalid_names_are_rejected_before_transport()
    {
        var container = CreateContainer(new AzureTableMemoryHandler(), out _);

        await Assert.That(async () => await container.TryGetAsync(" "))
            .Throws<ArgumentException>();
        await Assert.That(async () => await container.DeleteAsync(string.Empty))
            .Throws<ArgumentException>();
        await Assert.That(async () => await container.UpsertAsync(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(async () => await container.UpsertAsync(new AzureBlobRecord()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Registration_exposes_typed_container_and_blob_client()
    {
        var services = new ServiceCollection();
        services.AddStorage(builder => builder.AddAzureBlobStorage(options =>
        {
            options.ConnectionString = "UseDevelopmentStorage=true";
            options.ContainerName = ContainerName;
        }));

        using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<AzureBlobStorageOptions>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<BlobServiceClient>()).IsNotNull();
        await Assert.That(provider.GetRequiredService<IBlobContainer<AzureBlobRecord>>()).IsNotNull();
    }

    [Test]
    public async Task Credential_registration_and_option_validation_are_explicit()
    {
        var credential = new StaticCredential();
        var services = new ServiceCollection();
        services.AddStorage(builder => builder.AddAzureBlobStorage(options =>
        {
            options.Credential = credential;
            options.BlobServiceUri = new Uri("https://blob.example");
            options.ContainerName = ContainerName;
        }));

        using var provider = services.BuildServiceProvider();
        await Assert.That(provider.GetRequiredService<BlobServiceClient>()).IsNotNull();

        await Assert.That(() => Register(_ => { })).Throws<ArgumentException>();
        await Assert.That(() => Register(options => options.Credential = credential))
            .Throws<ArgumentException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.ContainerName = "Bad_Name";
        })).Throws<ArgumentException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.MaxPerPage = 0;
        })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => Register(options =>
        {
            options.ConnectionString = "memory";
            options.SerializerOptions = null!;
        })).Throws<ArgumentNullException>();
        await Assert.That(() => new ServiceCollection().AddStorage(builder =>
            builder.AddAzureBlobStorage(null!))).Throws<ArgumentNullException>();
        await Assert.That(() => new ServiceCollection().AddStorage(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Container_name_rules_cover_azure_boundaries()
    {
        await Assert.That(AzureBlobNames.IsValidContainerName("abc")).IsTrue();
        await Assert.That(AzureBlobNames.IsValidContainerName("a-b2")).IsTrue();
        await Assert.That(AzureBlobNames.IsValidContainerName("Abc")).IsFalse();
        await Assert.That(AzureBlobNames.IsValidContainerName("-abc")).IsFalse();
        await Assert.That(AzureBlobNames.IsValidContainerName("abc-")).IsFalse();
        await Assert.That(AzureBlobNames.IsValidContainerName("ab")).IsFalse();
        await Assert.That(AzureBlobNames.IsValidContainerName(new string('a', 64))).IsFalse();
        await Assert.That(AzureBlobNames.IsValidContainerName("a_b")).IsFalse();
    }

    private static AzureBlobContainer<AzureBlobRecord> CreateContainer(
        AzureTableMemoryHandler handler,
        out BlobServiceClient service)
    {
        var clientOptions = new BlobClientOptions
        {
            Transport = new HttpClientTransport(handler),
        };
        clientOptions.Retry.MaxRetries = 0;
        service = new BlobServiceClient(
            new Uri("https://novolis.blob.core.windows.net"),
            new StorageSharedKeyCredential(
                "devstoreaccount1",
                "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="),
            clientOptions);
        return new AzureBlobContainer<AzureBlobRecord>(
            service,
            new AzureBlobStorageOptions
            {
                ConnectionString = "memory",
                ContainerName = ContainerName,
                MaxPerPage = 1,
            });
    }

    private static void Register(Action<AzureBlobStorageOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddStorage(builder => builder.AddAzureBlobStorage(configure));
    }

    private const string ContainerName = "blobtests";

    private sealed class StaticCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
