using Azure;
using Azure.Data.Tables;

namespace Novolis.Storage.AzureCombinedStorage;

internal sealed record AzureCombinedManifestSnapshot(
    TableEntity Entity,
    AzureCombinedManifest Manifest,
    ETag ETag);
