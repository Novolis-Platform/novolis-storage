using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureTables;

internal sealed class AzureTableRepository<T> : IAzureTableRepository<T> where T : class, IHasId
{
    private static readonly ConcurrentDictionary<string, TableClient> Ready = new(StringComparer.Ordinal);

    private readonly TableServiceClient _service;
    private readonly AzureTableOptions _options;
    private readonly string _tableName;

    public AzureTableRepository(TableServiceClient service, AzureTableOptions options)
    {
        _service = service;
        _options = options;
        _tableName = AzureTableNames.ForType(typeof(T), options.TablePrefix);
    }

    public IEnumerable<T> All() => QueryCoreAsync(predicate: null, CancellationToken.None).ToBlockingEnumerable();

    public async ValueTask<T?> TryGetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        var response = await table.GetEntityIfExistsAsync<TableEntity>(
            AzureTableKeys.Partition,
            AzureTableKeys.RowKey(id),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.HasValue ? AzureTableEntityMapper.ToObject<T>(response.Value!) : null;
    }

    public async ValueTask UpsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        await table.UpsertEntityAsync(AzureTableEntityMapper.ToTable(entity), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        var response = await table.DeleteEntityAsync(
            AzureTableKeys.Partition,
            AzureTableKeys.RowKey(id),
            ETag.All,
            cancellationToken).ConfigureAwait(false);
        return response.Status != 404;
    }

    public IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return QueryCoreAsync(predicate, cancellationToken);
    }

    internal IAzureTableQuery<T> CreateQuery() =>
        new AzureTableQuery<T>(new AzureTableQueryProvider(QueryRowsAsync, AzureTableEntityMapper.ToObject<T>), expression: null);

    private async IAsyncEnumerable<T> QueryCoreAsync(
        Expression<Func<T, bool>>? predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var filter = predicate is null
            ? AzureTableFilterTranslator.PartitionOnly()
            : AzureTableFilterTranslator.WithPartition(predicate);

        await foreach (var row in QueryRowsAsync(filter, select: null, cancellationToken).ConfigureAwait(false))
            yield return AzureTableEntityMapper.ToObject<T>(row);
    }

    private async IAsyncEnumerable<TableEntity> QueryRowsAsync(
        string filter,
        IReadOnlyList<string>? select,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var table = await GetTableAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var row in table.QueryAsync<TableEntity>(
            filter: filter,
            maxPerPage: _options.MaxPerPage,
            select: select,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    private async ValueTask<TableClient> GetTableAsync(CancellationToken cancellationToken)
    {
        var key = _options.ConnectionString + "\n" + _tableName;
        if (Ready.TryGetValue(key, out var cached))
            return cached;

        var created = _service.GetTableClient(_tableName);
        await created.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        return Ready.GetOrAdd(key, created);
    }
}
