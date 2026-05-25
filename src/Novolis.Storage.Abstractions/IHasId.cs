namespace Novolis.Storage.Abstractions;

/// <summary>
/// Marks an entity that has a unique identifier.
/// </summary>
public interface IHasId
{
    Guid Id { get; }
}
