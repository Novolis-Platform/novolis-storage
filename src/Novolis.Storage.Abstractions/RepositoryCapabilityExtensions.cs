namespace Novolis.Storage.Abstractions;

/// <summary>Typed access helpers for optional repository capabilities.</summary>
public static class RepositoryCapabilityExtensions
{
    public static TCapability? GetCapability<TCapability>(
        this IRepositoryCapabilityProvider provider)
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetCapability(typeof(TCapability)) as TCapability;
    }
}
