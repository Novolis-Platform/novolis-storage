namespace Novolis.Storage.Abstractions;

/// <summary>
/// Exposes optional repository capabilities without expanding <see cref="IRepository{T}"/>.
/// </summary>
public interface IRepositoryCapabilityProvider
{
    /// <summary>Returns the capability for <paramref name="capabilityType"/>, or <see langword="null"/>.</summary>
    object? GetCapability(Type capabilityType);
}
