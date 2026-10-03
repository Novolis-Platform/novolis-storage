using Azure.Data.Tables;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureTables;

internal sealed class AzureTableRepositoryProvider(TableServiceClient service, AzureTableOptions options) : IRepositoryProvider
{
    public IRepository<T> Create<T>() where T : class, IHasId => new AzureTableRepository<T>(service, options);
}
