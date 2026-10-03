using System.Security.Cryptography;
using System.Text;

namespace Novolis.Storage.AzureCombinedStorage;

internal static class AzureCombinedNames
{
    public static string ManifestTable(Type entityType, string? prefix) =>
        TableName((prefix ?? string.Empty) + (entityType.FullName ?? entityType.Name) + "Manifest");

    public static string IndexTable(Type entityType, string? prefix) =>
        TableName((prefix ?? string.Empty) + (entityType.FullName ?? entityType.Name) + "Index");

    public static string BlobContainer(string name, string? prefix)
    {
        var value = Lowercase((prefix ?? string.Empty) + name);
        if (value.Length < 3)
            value = value.PadRight(3, 'x');
        if (value.Length <= 63)
            return value;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8].ToLowerInvariant();
        return value[..54] + hash;
    }

    public static string EntityStorage(Type entityType) =>
        Sanitize(entityType.FullName ?? entityType.Name).ToLowerInvariant();

    private static string TableName(string value)
    {
        var sanitized = Sanitize(value);
        if (sanitized.Length == 0 || char.IsAsciiDigit(sanitized[0]))
            sanitized = "T" + sanitized;
        if (sanitized.Length < 3)
            sanitized = sanitized.PadRight(3, 'x');
        if (sanitized.Length <= 63)
            return sanitized;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8];
        return sanitized[..55] + hash;
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    private static string Lowercase(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
                builder.Append(character);
        }

        return builder.ToString();
    }
}
