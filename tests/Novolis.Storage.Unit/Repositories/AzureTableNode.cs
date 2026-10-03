namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableNode
{
    public string Name { get; set; } = string.Empty;

    public AzureTableNode? Child { get; set; }
}
