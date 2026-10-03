using System.Globalization;
using System.Reflection;

namespace Novolis.Storage.AzureTables;

internal static class AzureTableValues
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "PartitionKey",
        "RowKey",
        "Timestamp",
        "ETag",
    };

    public static void EnsureMappable(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead || !property.CanWrite)
                continue;

            if (property.Name == nameof(Abstractions.IHasId.Id))
                continue;

            if (Reserved.Contains(property.Name))
                throw new NotSupportedException($"Property '{property.Name}' conflicts with an Azure Table system property.");
        }
    }

    public static bool IsReserved(PropertyInfo property) => Reserved.Contains(property.Name);

    public static bool IsStoredProperty(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length > 0 || !property.CanRead || !property.CanWrite)
            return false;

        if (property.Name == nameof(Abstractions.IHasId.Id))
            return false;

        EnsureStored(property);
        return true;
    }

    public static void EnsureStored(PropertyInfo property)
    {
        if (Reserved.Contains(property.Name))
            throw new NotSupportedException($"Property '{property.Name}' conflicts with an Azure Table system property.");
    }

    public static object Normalize(Type propertyType, object value)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type.IsEnum)
            return Enum.GetName(type, value) ?? value.ToString()!;

        return type switch
        {
            _ when type == typeof(string) && value is string text => text,
            _ when type == typeof(bool) && value is bool boolean => boolean,
            _ when type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int)
                => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            _ when type == typeof(uint) || type == typeof(long)
                => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            _ when type == typeof(float) && value is float single => (double)single,
            _ when type == typeof(double) && value is double number => number,
            _ when type == typeof(Guid) && value is Guid id => id,
            _ when type == typeof(DateTime) && value is DateTime dateTime => ToUtc(dateTime),
            _ when type == typeof(DateTimeOffset) && value is DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            _ => throw new InvalidOperationException($"Property type '{propertyType}' is not supported by Azure Table storage."),
        };
    }

    public static object Read(Type propertyType, object raw)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(string))
            return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;

        if (type == typeof(bool))
            return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);

        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int))
            return Convert.ChangeType(Convert.ToInt32(raw, CultureInfo.InvariantCulture), type, CultureInfo.InvariantCulture);

        if (type == typeof(uint) || type == typeof(long))
            return Convert.ChangeType(Convert.ToInt64(raw, CultureInfo.InvariantCulture), type, CultureInfo.InvariantCulture);

        if (type == typeof(double))
            return Convert.ToDouble(raw, CultureInfo.InvariantCulture);

        if (type == typeof(float))
            return Convert.ToSingle(raw, CultureInfo.InvariantCulture);

        if (type == typeof(Guid))
            return raw is Guid guid ? guid : Guid.Parse(Convert.ToString(raw, CultureInfo.InvariantCulture)!);

        if (type == typeof(DateTimeOffset))
        {
            return raw switch
            {
                DateTimeOffset dateTimeOffset => dateTimeOffset,
                DateTime dateTime => ToUtc(dateTime),
                _ => DateTimeOffset.Parse(
                    Convert.ToString(raw, CultureInfo.InvariantCulture)!,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
            };
        }

        if (type == typeof(DateTime))
            return ((DateTimeOffset)Read(typeof(DateTimeOffset), raw)).UtcDateTime;

        if (type.IsEnum)
            return Enum.Parse(type, Convert.ToString(raw, CultureInfo.InvariantCulture)!, ignoreCase: false);

        throw new InvalidOperationException($"Property type '{propertyType}' is not supported by Azure Table storage.");
    }

    private static DateTimeOffset ToUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        return new DateTimeOffset(utc);
    }
}
