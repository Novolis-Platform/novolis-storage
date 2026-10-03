using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureCombinedStorage;

/// <summary>Operational maintenance separate from normal repository operations.</summary>
public interface IAzureCombinedMaintenance<T> where T : class, IHasId
{
    ValueTask<int> RebuildIndexesAsync(CancellationToken cancellationToken = default);

    ValueTask<AzureCombinedMaintenanceReport> VerifyAsync(CancellationToken cancellationToken = default);

    ValueTask<int> RemoveStaleIndexEntriesAsync(CancellationToken cancellationToken = default);

    ValueTask<int> RemoveOrphansAsync(
        DateTimeOffset olderThan,
        CancellationToken cancellationToken = default);
}
