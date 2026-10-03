namespace Novolis.Storage.Query;

/// <summary>Thrown when a provider cannot execute a query without an unsafe scan or client fallback.</summary>
public sealed class QueryNotSupportedException : NotSupportedException
{
    public QueryNotSupportedException(string message)
        : base(message)
    {
    }
}
