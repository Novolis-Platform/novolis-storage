namespace Novolis.Storage.Abstractions;

/// <summary>Base62 encode/decode for time-sortable session directory names.</summary>
public static class SortableBase62
{
    public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string Encode(long value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Ticks must be non-negative.");

        if (value == 0)
            return Alphabet[0].ToString();

        Span<char> buffer = stackalloc char[12];
        var pos = buffer.Length;
        var v = value;
        while (v > 0)
        {
            var rem = (int)(v % 62);
            buffer[--pos] = Alphabet[rem];
            v /= 62;
        }

        return new string(buffer[pos..]);
    }

    public static long Decode(string base62)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base62);
        long v = 0;
        foreach (var c in base62)
        {
            var idx = Alphabet.IndexOf(c);
            if (idx < 0)
                throw new FormatException($"Invalid base62 character '{c}'.");
            v = checked(v * 62 + idx);
        }

        return v;
    }
}
