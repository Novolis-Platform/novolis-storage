using System.Linq.Expressions;

namespace Novolis.Storage.AzureTables;

/// <summary>
/// Composable Azure Table query. LINQ operators build an expression tree that is translated to
/// OData <c>filter</c> and <c>select</c>. Unsupported operators throw <see cref="NotSupportedException"/>
/// and are not evaluated on the client. Enumerate with <c>await foreach</c>.
/// </summary>
public interface IAzureTableQuery<T> : IQueryable<T>, IAsyncEnumerable<T>;
