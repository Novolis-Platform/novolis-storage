using System.Security.Cryptography;
using System.Text;

namespace Novolis.Storage.AzureCombinedStorage;

internal static class AzureCombinedKeys
{
    public static string Shard(Guid id, int shardCount)
    {
        var hash = SHA256.HashData(id.ToByteArray());
        var value = BitConverter.ToUInt16(hash, 0) % shardCount;
        return "s" + value.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string RowKey(Guid id) => id.ToString("D");

    public static string BlobName(Type entityType, Guid id, Guid revision, int shardCount) =>
        AzureCombinedNames.EntityStorage(entityType)
        + "/"
        + Shard(id, shardCount)
        + "/"
        + id.ToString("N")
        + "/"
        + revision.ToString("N")
        + "/payload.json";

    public static string IndexPartition(string identity) =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(identity));

    public static string IndexRow(string encodedValue, Guid id, Guid revision) =>
        encodedValue + "|" + RowKey(id) + "|" + revision.ToString("D");
}
