namespace Novolis.Storage.Query;

/// <summary>A finite page of query results and an opaque token for the next page.</summary>
public sealed record QueryPage<T>
{
    public QueryPage(IReadOnlyList<T> Items, string? ContinuationToken)
    {
        this.Items = Items?.ToArray() ?? throw new ArgumentNullException(nameof(Items));
        this.ContinuationToken = ContinuationToken;
    }

    public IReadOnlyList<T> Items { get; }

    public string? ContinuationToken { get; }
}
