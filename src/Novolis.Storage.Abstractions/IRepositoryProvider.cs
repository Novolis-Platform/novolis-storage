namespace Novolis.Storage.Abstractions;

/// <summary>
/// Engine-agnostic hook that creates repositories for a specific backend.
/// Enables type-specific rebinding without exposing provider implementation types.
/// </summary>
public interface IRepositoryProvider
{
    IRepository<T> Create<T>() where T : class, IHasId;
}
