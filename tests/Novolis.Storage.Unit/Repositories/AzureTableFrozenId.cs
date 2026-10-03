using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableFrozenId : IHasId
{
    public Guid Id { get; }

    public string Name { get; set; } = string.Empty;
}
