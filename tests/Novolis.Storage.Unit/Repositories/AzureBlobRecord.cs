using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureBlobRecord : IHasName
{
    public string Name { get; init; } = string.Empty;

    public string? Value { get; init; }
}
