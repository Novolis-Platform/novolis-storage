using System.Linq.Expressions;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureTables;

/// <summary>LINQ entry point for an Azure Table <see cref="IRepository{T}"/>.</summary>
public static class AzureTableRepositoryExtensions
{
    /// <summary>Starts a server-translated query. The repository must be the Azure Table provider.</summary>
    public static IAzureTableQuery<T> Query<T>(this IRepository<T> repository)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (repository is not AzureTableRepository<T> tables)
            throw new NotSupportedException("Query() is supported on Azure Table repositories. Client-side evaluation is not performed.");

        return tables.CreateQuery();
    }

    /// <summary>Adds a filter. The predicate is translated immediately.</summary>
    public static IAzureTableQuery<T> Where<T>(this IAzureTableQuery<T> source, Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return Chain(source, Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [typeof(T)],
            source.Expression,
            Expression.Quote(predicate)));
    }

    /// <summary>Projects stored properties. The selector is translated immediately.</summary>
    public static IAzureTableQuery<TResult> Select<T, TResult>(
        this IAzureTableQuery<T> source,
        Expression<Func<T, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var call = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            [typeof(T), typeof(TResult)],
            source.Expression,
            Expression.Quote(selector));
        AzureTableQueryPlan.Parse(call);
        return (IAzureTableQuery<TResult>)source.Provider.CreateQuery<TResult>(call);
    }

    /// <summary>Stops after <paramref name="count"/> entities from the filtered page stream.</summary>
    public static IAzureTableQuery<T> Take<T>(this IAzureTableQuery<T> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Chain(source, Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Take),
            [typeof(T)],
            source.Expression,
            Expression.Constant(count)));
    }

    /// <summary>Azure Table Storage has no sort operator.</summary>
    public static IAzureTableQuery<T> OrderBy<T, TKey>(this IAzureTableQuery<T> source, Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        throw AzureTableFilterTranslator.Reject(nameof(Queryable.OrderBy));
    }

    /// <summary>Azure Table Storage has no sort operator.</summary>
    public static IAzureTableQuery<T> OrderByDescending<T, TKey>(
        this IAzureTableQuery<T> source,
        Expression<Func<T, TKey>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keySelector);
        throw AzureTableFilterTranslator.Reject(nameof(Queryable.OrderByDescending));
    }

    /// <summary>Azure Table Storage has no offset operator.</summary>
    public static IAzureTableQuery<T> Skip<T>(this IAzureTableQuery<T> source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = count;
        throw AzureTableFilterTranslator.Reject(nameof(Queryable.Skip));
    }

    private static IAzureTableQuery<T> Chain<T>(IAzureTableQuery<T> source, Expression call)
    {
        AzureTableQueryPlan.Parse(call);
        return (IAzureTableQuery<T>)source.Provider.CreateQuery<T>(call);
    }
}
