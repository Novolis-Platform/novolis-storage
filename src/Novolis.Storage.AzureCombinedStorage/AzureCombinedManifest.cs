namespace Novolis.Storage.AzureCombinedStorage;

internal sealed record AzureCombinedManifest(
    Guid EntityId,
    string EntityType,
    Guid Revision,
    int SchemaVersion,
    bool Deleted,
    string BlobName,
    DateTimeOffset PublishedAt);
