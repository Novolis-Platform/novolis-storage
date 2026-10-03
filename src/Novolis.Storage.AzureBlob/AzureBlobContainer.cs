using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureBlob;

internal sealed class AzureBlobContainer<T> : IBlobContainer<T>
    where T : IHasName
{
    private readonly BlobContainerClient _container;
    private readonly AzureBlobStorageOptions _options;
    private readonly SemaphoreSlim _readyGate = new(1, 1);
    private bool _ready;

    public AzureBlobContainer(
        BlobServiceClient blobs,
        AzureBlobStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(blobs);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _container = blobs.GetBlobContainerClient(options.ContainerName);
    }

    public async IAsyncEnumerable<T> ListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var page in _container
            .GetBlobsAsync(cancellationToken: cancellationToken)
            .AsPages(pageSizeHint: _options.MaxPerPage)
            .ConfigureAwait(false))
        {
            foreach (var blob in page.Values)
            {
                var value = await ReadAsync(blob.Name, cancellationToken).ConfigureAwait(false);
                if (value is not null)
                    yield return value;
            }
        }
    }

    public async ValueTask<T?> TryGetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        return await ReadAsync(name, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UpsertAsync(
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        var name = ValidateName(value.Name);
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

        var content = new BinaryData(
            JsonSerializer.SerializeToUtf8Bytes(value, _options.SerializerOptions));
        await _container.GetBlobClient(name).UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json; charset=utf-8",
                },
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var response = await _container.GetBlobClient(name).DeleteIfExistsAsync(
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value;
    }

    private async ValueTask<T?> ReadAsync(
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.GetBlobClient(name)
                .DownloadContentAsync(cancellationToken)
                .ConfigureAwait(false);
            using var stream = response.Value.Content.ToStream();
            var value = await JsonSerializer.DeserializeAsync<T>(
                stream,
                _options.SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (value is null)
                throw new InvalidDataException($"Blob '{name}' contains a null JSON value.");

            if (!string.Equals(value.Name, name, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Blob '{name}' contains a value named '{value.Name}'.");

            return value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return default;
        }
    }

    private async ValueTask EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_ready)
            return;

        await _readyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ready)
                return;

            await _container.CreateIfNotExistsAsync(
                cancellationToken: cancellationToken).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            _readyGate.Release();
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A blob name is required.", nameof(name));

        return name;
    }
}
