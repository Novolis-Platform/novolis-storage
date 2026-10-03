namespace Novolis.Storage.Query;

/// <summary>One finite comparison in a storage query.</summary>
public sealed record QueryPredicate
{
    public QueryPredicate(
        PropertyPath property,
        QueryComparisonOperator operation,
        IReadOnlyList<object?> values)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        Values = values?.ToArray() ?? throw new ArgumentNullException(nameof(values));
        Operator = operation;
        ValidateValueCount(Operator, Values.Count);
    }

    public PropertyPath Property { get; }

    public QueryComparisonOperator Operator { get; }

    public IReadOnlyList<object?> Values { get; }

    private static void ValidateValueCount(QueryComparisonOperator operation, int count)
    {
        var valid = operation == QueryComparisonOperator.IsIn
            ? count > 0
            : operation == QueryComparisonOperator.IsBetween
                ? count == 2
                : count == 1;
        if (!valid)
            throw new ArgumentException($"Operator '{operation}' received {count} values.");
    }
}
