using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Indexing;
using Novolis.Storage.Query;

namespace Novolis.Storage.AzureCombinedStorage;

/// <summary>
/// Preview repository whose manifest is the publication point for immutable
/// Table/Blob aggregate revisions.
/// </summary>
public sealed class AzureCombinedRepository<T> :
    IRepository<T>,
    IAzureCombinedMaintenance<T>,
    IRepositoryQueryProvider<T>
    where T : class, IHasId
{
    private readonly TableClient _manifestTable;
    private readonly TableClient _indexTable;
    private readonly BlobContainerClient _blobContainer;
    private readonly AzureCombinedStorageOptions _options;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _readyGate = new(1, 1);
    private bool _ready;

    public AzureCombinedRepository(
        TableServiceClient tables,
        BlobServiceClient blobs,
        AzureCombinedStorageOptions options,
        ILogger<AzureCombinedRepository<T>>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(blobs);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _manifestTable = tables.GetTableClient(AzureCombinedNames.ManifestTable(typeof(T), options.TablePrefix));
        _indexTable = tables.GetTableClient(AzureCombinedNames.IndexTable(typeof(T), options.TablePrefix));
        _blobContainer = blobs.GetBlobContainerClient(
            AzureCombinedNames.BlobContainer(options.BlobContainerName, options.TablePrefix));
    }

    public IEnumerable<T> All() => AllAsync(CancellationToken.None).ToBlockingEnumerable();

    public ValueTask<T?> TryGetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(id, cancellationToken);

    public async ValueTask<T?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);
        if (snapshot is null || snapshot.Manifest.Deleted)
            return null;

        return await ReadPayloadAsync(snapshot.Manifest, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UpsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var id = entity.Id;
        var current = await ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);
        var revision = Guid.CreateVersion7();
        var blobName = AzureCombinedKeys.BlobName(typeof(T), id, revision, _options.ShardCount);
        var payload = AzureCombinedSerializer.Serialize(entity);
        await _blobContainer.GetBlobClient(blobName)
            .UploadAsync(payload, overwrite: false, cancellationToken)
            .ConfigureAwait(false);

        foreach (var descriptor in _options.GetIndexes(typeof(T)))
        {
            foreach (var indexEntity in AzureCombinedIndexMapper.ToEntities(entity, descriptor, id, revision))
            {
                await _indexTable.UpsertEntityAsync(
                    indexEntity,
                    TableUpdateMode.Replace,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var manifest = new AzureCombinedManifest(
            id,
            typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            revision,
            _options.SchemaVersion,
            Deleted: false,
            blobName,
            DateTimeOffset.UtcNow);
        var manifestEntity = AzureCombinedManifestMapper.ToEntity(
            manifest,
            typeof(T),
            AzureCombinedKeys.Shard(id, _options.ShardCount));
        if (current is null)
        {
            await _manifestTable.AddEntityAsync(manifestEntity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _manifestTable.UpdateEntityAsync(
                manifestEntity,
                current.ETag,
                TableUpdateMode.Replace,
                cancellationToken).ConfigureAwait(false);
        }

        _logger?.LogDebug(
            "Published aggregate revision {Revision} for {EntityType} {EntityId}.",
            revision,
            typeof(T).Name,
            id);
    }

    public async ValueTask<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var current = await ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Manifest.Deleted)
            return false;

        var tombstone = new AzureCombinedManifest(
            id,
            current.Manifest.EntityType,
            Guid.CreateVersion7(),
            _options.SchemaVersion,
            Deleted: true,
            current.Manifest.BlobName,
            DateTimeOffset.UtcNow);
        var manifestEntity = AzureCombinedManifestMapper.ToEntity(
            tombstone,
            typeof(T),
            AzureCombinedKeys.Shard(id, _options.ShardCount));
        await _manifestTable.UpdateEntityAsync(
            manifestEntity,
            current.ETag,
            TableUpdateMode.Replace,
            cancellationToken).ConfigureAwait(false);
        _logger?.LogDebug(
            "Published tombstone revision {Revision} for {EntityType} {EntityId}.",
            tombstone.Revision,
            typeof(T).Name,
            id);
        return true;
    }

    public ValueTask<QueryPage<T>> ExecuteAsync(
        Query<T> query,
        CancellationToken cancellationToken = default) =>
        AzureCombinedQueryExecutor.ExecuteAsync(this, query, cancellationToken);

    public ValueTask<long> CountAsync(
        Query<T> query,
        CancellationToken cancellationToken = default) =>
        AzureCombinedQueryExecutor.CountAsync(this, query, cancellationToken);

    public ValueTask<int> RebuildIndexesAsync(CancellationToken cancellationToken = default) =>
        new AzureCombinedMaintenance<T>(this).RebuildIndexesAsync(cancellationToken);

    public ValueTask<AzureCombinedMaintenanceReport> VerifyAsync(
        CancellationToken cancellationToken = default) =>
        new AzureCombinedMaintenance<T>(this).VerifyAsync(cancellationToken);

    public ValueTask<int> RemoveStaleIndexEntriesAsync(
        CancellationToken cancellationToken = default) =>
        new AzureCombinedMaintenance<T>(this).RemoveStaleIndexEntriesAsync(cancellationToken);

    public ValueTask<int> RemoveOrphansAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default) =>
        new AzureCombinedMaintenance<T>(this).RemoveOrphansAsync(olderThan, cancellationToken);

    internal TableClient ManifestTable => _manifestTable;

    internal TableClient IndexTable => _indexTable;

    internal BlobContainerClient BlobContainer => _blobContainer;

    internal AzureCombinedStorageOptions Options => _options;

    internal IReadOnlyList<IndexDescriptor> Indexes =>
        _options.GetIndexes(typeof(T));

    internal void LogQuery(IndexPlanDiagnostic diagnostic, int candidates, int accepted) =>
        _logger?.LogDebug(
            "Storage query {Plan} for {EntityType}; index {IndexIdentity}; candidates {Candidates}; accepted {Accepted}; hydration {Hydration}.",
            diagnostic.Plan,
            diagnostic.EntityType.Name,
            diagnostic.IndexIdentity ?? "-",
            candidates,
            accepted,
            diagnostic.CandidateHydrationRequired);

    internal async ValueTask<AzureCombinedManifestSnapshot?> ReadManifestAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _manifestTable.GetEntityIfExistsAsync<TableEntity>(
            AzureCombinedKeys.Shard(id, _options.ShardCount),
            AzureCombinedKeys.RowKey(id),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.HasValue ? AzureCombinedManifestMapper.Read(response.Value!) : null;
    }

    internal async ValueTask<T> ReadPayloadAsync(
        AzureCombinedManifest manifest,
        CancellationToken cancellationToken)
    {
        var response = await _blobContainer.GetBlobClient(manifest.BlobName)
            .DownloadContentAsync(cancellationToken)
            .ConfigureAwait(false);
        return AzureCombinedSerializer.Deserialize<T>(response.Value.Content);
    }

    internal async IAsyncEnumerable<AzureCombinedManifestSnapshot> ReadManifestsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var shard = 0; shard < _options.ShardCount; shard++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partition = "s" + shard.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partition}");
            await foreach (var entity in _manifestTable.QueryAsync<TableEntity>(
                filter,
                maxPerPage: _options.MaxPerPage,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                yield return AzureCombinedManifestMapper.Read(entity);
            }
        }
    }

    internal async ValueTask EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_ready)
            return;

        await _readyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ready)
                return;
            await _manifestTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            await _indexTable.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            await _blobContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            _readyGate.Release();
        }
    }

    private async IAsyncEnumerable<T> AllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var snapshot in ReadManifestsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!snapshot.Manifest.Deleted)
                yield return await ReadPayloadAsync(snapshot.Manifest, cancellationToken).ConfigureAwait(false);
        }
    }
}
