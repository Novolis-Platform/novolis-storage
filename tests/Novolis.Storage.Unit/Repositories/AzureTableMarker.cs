using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableMarker : IHasId
{
    public Guid Id { get; set; }

    public string Label { get; set; } = string.Empty;
}
