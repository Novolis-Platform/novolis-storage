using System.Text;

namespace Novolis.Storage.Query;

/// <summary>Creates and validates opaque query continuation tokens.</summary>
public static class QueryContinuation
{
    public static string Create(string fingerprint, int offset)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            throw new ArgumentException("A query fingerprint is required.", nameof(fingerprint));
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        var payload = fingerprint + "|" + offset;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryRead(string token, string fingerprint, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(fingerprint))
            return false;

        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var separator = payload.LastIndexOf('|');
            if (separator <= 0 || !string.Equals(payload[..separator], fingerprint, StringComparison.Ordinal))
                return false;

            return int.TryParse(payload[(separator + 1)..], out offset) && offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
