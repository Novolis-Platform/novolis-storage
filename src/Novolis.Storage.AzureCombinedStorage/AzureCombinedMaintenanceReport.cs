namespace Novolis.Storage.AzureCombinedStorage;

/// <summary>Counts produced by one consistency verification pass.</summary>
public sealed record AzureCombinedMaintenanceReport(
    int ActiveManifests,
    int MissingBlobs,
    int StaleIndexEntries,
    int RemovedIndexEntries,
    int RebuiltIndexEntries);
