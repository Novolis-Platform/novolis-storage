using System.Collections;
using System.Reflection;
using Novolis.Storage.Query;

namespace Novolis.Storage.Indexing;

/// <summary>Reads one or more values from a nested property path for index extraction.</summary>
public static class IndexValueAccessor
{
    public static IReadOnlyList<object?> Read(object entity, PropertyPath path)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(path);
        var values = new List<object?>();
        Visit(entity, path.Segments, 0, values);
        return values;
    }

    private static void Visit(object? current, IReadOnlyList<string> segments, int index, List<object?> values)
    {
        if (index == segments.Count)
        {
            values.Add(current);
            return;
        }

        if (current is null)
        {
            values.Add(null);
            return;
        }

        if (current is IEnumerable sequence and not string and not byte[])
        {
            foreach (var item in sequence)
                Visit(item, segments, index, values);
            return;
        }

        var property = current.GetType().GetProperty(
            segments[index],
            BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanRead || property.GetIndexParameters().Length != 0)
            return;

        Visit(property.GetValue(current), segments, index + 1, values);
    }
}
