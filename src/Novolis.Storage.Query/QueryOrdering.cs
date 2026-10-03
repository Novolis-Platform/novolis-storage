namespace Novolis.Storage.Query;

/// <summary>One property path and direction used to order a query.</summary>
public sealed record QueryOrdering
{
    public QueryOrdering(PropertyPath property, QueryOrderDirection direction)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        Direction = direction;
    }

    public PropertyPath Property { get; }

    public QueryOrderDirection Direction { get; }
}
