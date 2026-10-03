using System.Globalization;
using Azure;
using Azure.Data.Tables;

namespace Novolis.Storage.AzureCombinedStorage;

internal static class AzureCombinedManifestMapper
{
    public static TableEntity ToEntity(AzureCombinedManifest manifest, Type entityType, string partitionKey)
    {
        return new TableEntity(partitionKey, AzureCombinedKeys.RowKey(manifest.EntityId))
        {
            ["EntityType"] = manifest.EntityType,
            ["Revision"] = manifest.Revision,
            ["SchemaVersion"] = manifest.SchemaVersion,
            ["Deleted"] = manifest.Deleted,
            ["BlobName"] = manifest.BlobName,
            ["PublishedAt"] = manifest.PublishedAt,
            ["StorageIdentity"] = AzureCombinedNames.EntityStorage(entityType),
        };
    }

    public static AzureCombinedManifestSnapshot Read(TableEntity entity)
    {
        var manifest = new AzureCombinedManifest(
            Guid.Parse(entity.RowKey),
            Convert.ToString(entity["EntityType"], CultureInfo.InvariantCulture) ?? string.Empty,
            ReadGuid(entity["Revision"]),
            Convert.ToInt32(entity["SchemaVersion"], CultureInfo.InvariantCulture),
            Convert.ToBoolean(entity["Deleted"], CultureInfo.InvariantCulture),
            Convert.ToString(entity["BlobName"], CultureInfo.InvariantCulture) ?? string.Empty,
            ReadDate(entity["PublishedAt"]));
        return new AzureCombinedManifestSnapshot(entity, manifest, entity.ETag);
    }

    private static Guid ReadGuid(object value) =>
        value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);

    private static DateTimeOffset ReadDate(object value) =>
        value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => DateTimeOffset.Parse(
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        };
}
