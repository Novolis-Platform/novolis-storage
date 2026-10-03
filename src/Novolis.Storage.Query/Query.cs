using System.Linq.Expressions;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Query;

/// <summary>Immutable description of a finite, provider-executable storage query.</summary>
public sealed class Query<T> where T : class, IHasId
{
    private readonly IReadOnlyList<QueryPredicate> _predicates;
    private readonly IReadOnlyList<QueryOrdering> _ordering;

    internal Query(
        IRepositoryQueryProvider<T> provider,
        IReadOnlyList<QueryPredicate>? predicates = null,
        IReadOnlyList<QueryOrdering>? ordering = null,
        int? limit = null,
        string? continuationToken = null)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _predicates = predicates?.ToArray() ?? [];
        _ordering = ordering?.ToArray() ?? [];
        Limit = limit;
        ContinuationToken = continuationToken;
    }

    internal IRepositoryQueryProvider<T> Provider { get; }

    public IReadOnlyList<QueryPredicate> Predicates => _predicates;

    public IReadOnlyList<QueryOrdering> Ordering => _ordering;

    public int? Limit { get; }

    public string? ContinuationToken { get; }

    public QueryPredicateBuilder<T, TValue> Where<TValue>(Expression<Func<T, TValue>> selector) =>
        new(this, selector);

    public QueryPredicateBuilder<T, TValue> And<TValue>(Expression<Func<T, TValue>> selector) =>
        new(this, selector);

    public Query<T> OrderBy<TValue>(Expression<Func<T, TValue>> selector) =>
        AddOrdering(selector, QueryOrderDirection.Ascending);

    public Query<T> OrderByDescending<TValue>(Expression<Func<T, TValue>> selector) =>
        AddOrdering(selector, QueryOrderDirection.Descending);

    public Query<T> Take(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var limit = Limit is int current ? Math.Min(current, count) : count;
        return new Query<T>(Provider, _predicates, _ordering, limit, ContinuationToken);
    }

    public Query<T> WithContinuation(string? continuationToken) =>
        new(Provider, _predicates, _ordering, Limit, continuationToken);

    public Query<T> Continuation(string? continuationToken) => WithContinuation(continuationToken);

    public ValueTask<QueryPage<T>> ToPageAsync(CancellationToken cancellationToken = default) =>
        Provider.ExecuteAsync(this, cancellationToken);

    public async ValueTask<IReadOnlyList<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        var current = this;
        while (true)
        {
            var page = await current.ToPageAsync(cancellationToken).ConfigureAwait(false);
            items.AddRange(page.Items);
            if (page.ContinuationToken is null)
                return items;

            current = current.WithContinuation(page.ContinuationToken);
        }
    }

    public async ValueTask<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var page = await Take(1).ToPageAsync(cancellationToken).ConfigureAwait(false);
        return page.Items.Count == 0 ? null : page.Items[0];
    }

    public async ValueTask<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var page = await Take(1).ToPageAsync(cancellationToken).ConfigureAwait(false);
        return page.Items.Count != 0;
    }

    public ValueTask<long> CountAsync(CancellationToken cancellationToken = default) =>
        Provider.CountAsync(this, cancellationToken);

    internal Query<T> Append(QueryPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var predicates = _predicates.Append(predicate).ToArray();
        return new Query<T>(Provider, predicates, _ordering, Limit, ContinuationToken);
    }

    private Query<T> AddOrdering<TValue>(
        Expression<Func<T, TValue>> selector,
        QueryOrderDirection direction)
    {
        var property = PropertyPathParser.Parse(selector);
        var ordering = _ordering.Append(new QueryOrdering(property, direction)).ToArray();
        return new Query<T>(Provider, _predicates, ordering, Limit, ContinuationToken);
    }
}
