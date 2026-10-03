using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableOdd : IHasId
{
    public Guid Id { get; set; }

    public bool flagField;

    public string Timestamp { get; set; } = string.Empty;
}
