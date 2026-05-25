using Microsoft.Extensions.DependencyInjection;
using Novolis.IO.Workspace;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Json;

internal sealed class JsonFilesRepositoryProvider(
    JsonFilesOptions options,
    [FromKeyedServices(JsonStorageWorkspaceKey.Default)] IFileWorkspace workspace) : IRepositoryProvider
{
    public IRepository<T> Create<T>() where T : class, IHasId =>
        new JsonFilesRepository<T>(options, workspace);
}
