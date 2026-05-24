namespace Novolis.Storage.Abstractions;

/// <summary>
/// Human-readable type name formatting for storage metadata.
/// </summary>
public static class TypeNameExtensions
{
    /// <summary>
    /// Returns a display name for the type, expanding generic arguments.
    /// </summary>
    /// <param name="type">Type to format.</param>
    /// <returns>Display name suitable for logs and table names.</returns>
    public static string GetDisplayName(this Type type)
    {
        if (!type.IsConstructedGenericType)
            return type.Name;

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];

        return name + "Of" + string.Join("And", type.GenericTypeArguments.Select(GetDisplayName));
    }
}
