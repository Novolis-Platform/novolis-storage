using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class StorageNestedEntity : IHasId
{
    public Guid Id { get; set; }

    public StorageNestedDetails Details { get; set; } = new();
}
