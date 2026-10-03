using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureCombinedStorage;

internal sealed class AzureCombinedMaintenance<T>(
    AzureCombinedRepository<T> repository) : IAzureCombinedMaintenance<T>
    where T : class, IHasId
{
    public async ValueTask<int> RebuildIndexesAsync(CancellationToken cancellationToken = default)
    {
        await repository.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var count = 0;
        await foreach (var manifest in repository.ReadManifestsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (manifest.Manifest.Deleted)
                continue;

            var entity = await repository.ReadPayloadAsync(manifest.Manifest, cancellationToken).ConfigureAwait(false);
            foreach (var descriptor in repository.Indexes)
            {
                foreach (var row in AzureCombinedIndexMapper.ToEntities(
                    entity,
                    descriptor,
                    manifest.Manifest.EntityId,
                    manifest.Manifest.Revision))
                {
                    await repository.IndexTable.UpsertEntityAsync(
                        row,
                        TableUpdateMode.Replace,
                        cancellationToken).ConfigureAwait(false);
                    count++;
                }
            }
        }

        return count;
    }

    public async ValueTask<AzureCombinedMaintenanceReport> VerifyAsync(
        CancellationToken cancellationToken = default)
    {
        await repository.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var activeManifests = 0;
        var missingBlobs = 0;
        await foreach (var manifest in repository.ReadManifestsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (manifest.Manifest.Deleted)
                continue;

            activeManifests++;
            var exists = false;
            try
            {
                exists = (await repository.BlobContainer.GetBlobClient(manifest.Manifest.BlobName)
                        .ExistsAsync(cancellationToken)
                        .ConfigureAwait(false)).Value;
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                // Some Azure-compatible services surface a missing HEAD as a
                // request failure instead of the SDK's false Exists result.
            }

            if (!exists)
            {
                missingBlobs++;
            }
        }

        var stale = await CountStaleIndexEntriesAsync(cancellationToken).ConfigureAwait(false);
        return new AzureCombinedMaintenanceReport(
            activeManifests,
            missingBlobs,
            stale,
            RemovedIndexEntries: 0,
            RebuiltIndexEntries: 0);
    }

    public async ValueTask<int> RemoveStaleIndexEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        await repository.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var removed = 0;
        await foreach (var row in repository.IndexTable.QueryAsync<TableEntity>(
            maxPerPage: repository.Options.MaxPerPage,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!await IsCurrentAsync(row, cancellationToken).ConfigureAwait(false))
            {
                await repository.IndexTable.DeleteEntityAsync(
                    row.PartitionKey,
                    row.RowKey,
                    ETag.All,
                    cancellationToken).ConfigureAwait(false);
                removed++;
            }
        }

        return removed;
    }

    public async ValueTask<int> RemoveOrphansAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default)
    {
        await repository.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var removed = await RemoveStaleIndexEntriesAsync(cancellationToken).ConfigureAwait(false);
        var activeBlobs = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var manifest in repository.ReadManifestsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!manifest.Manifest.Deleted)
                activeBlobs.Add(manifest.Manifest.BlobName);
        }

        await foreach (BlobItem blob in repository.BlobContainer.GetBlobsAsync(cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            var modified = blob.Properties.LastModified;
            if (modified is null
                || modified >= olderThan
                || activeBlobs.Contains(blob.Name))
                continue;

            if (await repository.BlobContainer.DeleteBlobIfExistsAsync(
                    blob.Name,
                    cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                removed++;
            }
        }

        return removed;
    }

    private async ValueTask<int> CountStaleIndexEntriesAsync(CancellationToken cancellationToken)
    {
        var stale = 0;
        await foreach (var row in repository.IndexTable.QueryAsync<TableEntity>(
            maxPerPage: repository.Options.MaxPerPage,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!await IsCurrentAsync(row, cancellationToken).ConfigureAwait(false))
                stale++;
        }

        return stale;
    }

    private async ValueTask<bool> IsCurrentAsync(
        TableEntity row,
        CancellationToken cancellationToken)
    {
        if (!row.TryGetValue("EntityId", out var entityId)
            || !row.TryGetValue("Revision", out var revision))
            return false;

        var id = ReadGuid(entityId);
        var candidateRevision = ReadGuid(revision);
        var manifest = await repository.ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);
        return manifest is not null
            && !manifest.Manifest.Deleted
            && manifest.Manifest.Revision == candidateRevision;
    }

    private static Guid ReadGuid(object? value) =>
        value is Guid guid
            ? guid
            : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
}
