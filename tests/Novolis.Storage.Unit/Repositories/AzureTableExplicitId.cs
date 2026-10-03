using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableExplicitId : IHasId
{
    public string Name { get; set; } = string.Empty;

    Guid IHasId.Id => Guid.Empty;
}
