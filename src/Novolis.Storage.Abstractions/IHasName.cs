namespace Novolis.Storage.Abstractions;

/// <summary>
/// Marks a value whose stable name identifies its storage entry.
/// </summary>
public interface IHasName
{
    string Name { get; }
}
