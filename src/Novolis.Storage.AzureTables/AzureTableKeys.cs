namespace Novolis.Storage.AzureTables;

internal static class AzureTableKeys
{
    public const string Partition = "row";

    public static string RowKey(Guid id) => id.ToString("D");
}
