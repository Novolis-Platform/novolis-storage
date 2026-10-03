using Novolis.Storage.Abstractions;

namespace Novolis.Storage.AzureBlob;

/// <summary>
/// Typed access to JSON values stored as named Azure blobs.
/// </summary>
/// <typeparam name="T">The value type stored in the container.</typeparam>
public interface IBlobContainer<T>
    where T : IHasName
{
    /// <summary>
    /// Lists every readable value in the container, following Azure continuation pages.
    /// </summary>
    IAsyncEnumerable<T> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the value stored under <paramref name="name"/>, or returns <see langword="null"/>
    /// when the blob does not exist.
    /// </summary>
    ValueTask<T?> TryGetAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes and replaces the blob identified by <see cref="IHasName.Name"/>.
    /// </summary>
    ValueTask UpsertAsync(
        T value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the blob identified by <paramref name="name"/>.
    /// </summary>
    ValueTask<bool> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default);
}
