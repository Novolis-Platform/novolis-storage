using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureCombinedStorage;

internal sealed class AzureCombinedRepositoryProvider(
    TableServiceClient tables,
    BlobServiceClient blobs,
    AzureCombinedStorageOptions options) : IRepositoryProvider
{
    public IRepository<T> Create<T>() where T : class, IHasId =>
        new AzureCombinedRepository<T>(tables, blobs, options);
}
