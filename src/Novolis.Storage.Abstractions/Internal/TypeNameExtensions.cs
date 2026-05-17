namespace Novolis.Storage.Abstractions;

public static class TypeNameExtensions
{
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
