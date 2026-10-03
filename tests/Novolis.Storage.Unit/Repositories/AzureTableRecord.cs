using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableRecord : IHasId
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Note { get; set; }

    public bool Active { get; set; }

    public int Count { get; set; }

    public Guid Token { get; set; }

    public DateTimeOffset When { get; set; }

    public AzureTableStatus Status { get; set; }
}
