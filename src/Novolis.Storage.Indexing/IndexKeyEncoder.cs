using System.Globalization;
using System.Text;
using Novolis.Storage.Query;

namespace Novolis.Storage.Indexing;

/// <summary>Deterministic, culture-independent encoding for physical index keys.</summary>
public static class IndexKeyEncoder
{
    public static string Encode(object? value)
    {
        return value switch
        {
            null => "0",
            string text => "s" + Hex(Encoding.UTF8.GetBytes(text)),
            bool boolean => "b" + (boolean ? "1" : "0"),
            byte number => "u" + number.ToString("X16", CultureInfo.InvariantCulture),
            ushort number => "u" + number.ToString("X16", CultureInfo.InvariantCulture),
            uint number => "u" + number.ToString("X16", CultureInfo.InvariantCulture),
            ulong number => "u" + number.ToString("X16", CultureInfo.InvariantCulture),
            sbyte number => Signed(number),
            short number => Signed(number),
            int number => Signed(number),
            long number => Signed(number),
            float number => Floating(number),
            double number => Floating(number),
            decimal number => "d" + number.ToString("G29", CultureInfo.InvariantCulture),
            Guid guid => "g" + guid.ToString("N"),
            DateTime dateTime => Signed(dateTime.ToUniversalTime().Ticks),
            DateTimeOffset dateTimeOffset => Signed(dateTimeOffset.UtcTicks),
            DateOnly dateOnly => Signed(dateOnly.DayNumber),
            TimeOnly timeOnly => Signed(timeOnly.Ticks),
            Enum enumeration => "e" + Hex(Encoding.UTF8.GetBytes(enumeration.ToString())),
            byte[] bytes => "x" + Hex(bytes),
            _ => "o" + Hex(Encoding.UTF8.GetBytes(
                value is IFormattable formattable
                    ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
                    : value.ToString() ?? string.Empty)),
        };
    }

    public static string EncodePath(PropertyPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Hex(Encoding.UTF8.GetBytes(path.Value));
    }

    public static string LowerBound(object? value) => Encode(value) + "|";

    public static string UpperBound(object? value) => Encode(value) + "|~";

    private static string Signed(long value)
    {
        var sortable = unchecked((ulong)(value ^ long.MinValue));
        return "n" + sortable.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static string Floating(double value)
    {
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        var sortable = (bits & 0x8000000000000000UL) != 0
            ? ~bits
            : bits ^ 0x8000000000000000UL;
        return "f" + sortable.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);
}
