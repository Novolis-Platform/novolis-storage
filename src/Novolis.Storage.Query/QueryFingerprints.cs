using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Query;

/// <summary>Creates stable fingerprints used to bind opaque continuation tokens to a query.</summary>
public static class QueryFingerprints
{
    public static string Compute<T>(Query<T> query)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(query);
        var builder = new StringBuilder(typeof(T).AssemblyQualifiedName);
        foreach (var predicate in query.Predicates)
        {
            builder.Append("|p:");
            builder.Append(predicate.Property.Value);
            builder.Append(':');
            builder.Append(predicate.Operator);
            foreach (var value in predicate.Values)
            {
                builder.Append(':');
                builder.Append(StableValue(value));
            }
        }

        foreach (var ordering in query.Ordering)
        {
            builder.Append("|o:");
            builder.Append(ordering.Property.Value);
            builder.Append(':');
            builder.Append(ordering.Direction);
        }

        builder.Append("|take:");
        builder.Append(query.Limit?.ToString(CultureInfo.InvariantCulture) ?? "-");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string StableValue(object? value)
    {
        if (value is null)
            return "<null>";

        if (value is string text)
            return "String:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        if (value is IFormattable formattable)
            return value.GetType().AssemblyQualifiedName + ":" + formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.GetType().AssemblyQualifiedName + ":" + value;
    }
}
