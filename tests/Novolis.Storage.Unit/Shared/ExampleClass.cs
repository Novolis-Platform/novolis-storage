using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Tests.Shared;

public sealed class ExampleClass : IHasId
{
    public Guid Id { get; set; }
    public string SomeData { get; set; } = string.Empty;
    public bool Boolean { get; set; }
}
