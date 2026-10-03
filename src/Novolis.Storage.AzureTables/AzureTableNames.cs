using System.Security.Cryptography;
using System.Text;

namespace Novolis.Storage.AzureTables;

internal static class AzureTableNames
{
    public static string ForType(Type type, string? prefix)
    {
        var identity = string.IsNullOrWhiteSpace(prefix)
            ? type.FullName ?? type.Name
            : prefix.Trim() + "." + (type.FullName ?? type.Name);
        return FromIdentity(identity);
    }

    public static string FromIdentity(string identity)
    {
        var builder = new StringBuilder(identity.Length);
        foreach (var character in identity)
        {
            if (char.IsAsciiLetterOrDigit(character))
                builder.Append(character);
        }

        if (builder.Length == 0 || char.IsAsciiDigit(builder[0]))
            builder.Insert(0, 'T');

        while (builder.Length < 3)
            builder.Append('x');

        if (builder.Length <= 63)
            return builder.ToString();

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..8];
        return string.Concat(builder.ToString().AsSpan(0, 55), hash);
    }
}
