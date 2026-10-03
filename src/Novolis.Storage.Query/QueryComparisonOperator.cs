namespace Novolis.Storage.Query;

/// <summary>Comparison operators supported by the finite storage query language.</summary>
public enum QueryComparisonOperator
{
    Is,
    IsNot,
    IsGreaterThan,
    IsGreaterThanOrEqual,
    IsLessThan,
    IsLessThanOrEqual,
    IsBetween,
    StartsWith,
    IsIn,
}
