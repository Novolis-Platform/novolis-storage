using Microsoft.Extensions.DependencyInjection;

namespace Novolis.Storage.Abstractions;

/// <summary>
/// Extensions for binding a specific entity type to a specific repository provider.
/// Register the closed generic after the default open-generic provider so the override wins.
/// </summary>
public static class StorageTypeBindingExtensions
{
    /// <summary>
    /// Binds entity type <typeparamref name="T"/> to the repository created by <typeparamref name="TProvider"/>.
    /// </summary>
    /// <typeparam name="T">The entity type (must implement <see cref="IHasId"/>).</typeparam>
    /// <typeparam name="TProvider">The provider type that implements <see cref="IRepositoryProvider"/>.</typeparam>
    /// <param name="builder">The storage builder.</param>
    /// <returns>The same builder for chaining.</returns>
    public static IStorageBuilder BindRepository<T, TProvider>(this IStorageBuilder builder)
        where T : class, IHasId
        where TProvider : class, IRepositoryProvider
    {
        builder.Services.AddTransient<IRepository<T>>(sp =>
            sp.GetRequiredService<TProvider>().Create<T>());

        return builder;
    }
}
