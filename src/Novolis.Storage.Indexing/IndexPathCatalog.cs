using System.Collections;
using System.Reflection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Query;

namespace Novolis.Storage.Indexing;

/// <summary>Discovers conservative scalar paths that can be indexed automatically.</summary>
public static class IndexPathCatalog
{
    public static IReadOnlyList<PropertyPath> Discover(Type entityType, int maxDepth = 8)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
        var paths = new List<PropertyPath>();
        Visit(entityType, [], paths, maxDepth);
        return paths;
    }

    private static void Visit(Type type, IReadOnlyList<string> prefix, List<PropertyPath> paths, int remaining)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (remaining == 0 || IsScalar(type))
            return;

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0
                || !property.CanRead
                || property.Name == nameof(IHasId.Id))
                continue;

            var path = prefix.Append(property.Name).ToArray();
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (IsScalar(propertyType))
            {
                paths.Add(new PropertyPath(path));
                continue;
            }

            if (propertyType != type && !typeof(IDictionary).IsAssignableFrom(propertyType))
                Visit(propertyType, path, paths, remaining - 1);
        }
    }

    public static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsEnum
            || type == typeof(string)
            || type == typeof(bool)
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(byte[]);
    }
}
