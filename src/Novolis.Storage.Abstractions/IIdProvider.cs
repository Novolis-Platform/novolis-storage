namespace Novolis.Storage.Abstractions;

/// <summary>
/// Provides new unique identifiers for entities.
/// </summary>
public interface IIdProvider
{
    Guid NewId();
}
