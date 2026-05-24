using Novolis.Storage.Abstractions;

using Microsoft.Extensions.Options;

namespace Novolis.Storage.Json;

/// <summary>
/// Provides typed <see cref="JsonTable{T}"/> instances backed by the configured data folder.
/// </summary>
public class JsonContext
{
    private readonly DirectoryInfo _directoryInfo;

    /// <summary>
    /// Creates the root data directory when missing.
    /// </summary>
    /// <param name="options">Connection options containing <see cref="JsonConnection.JsonDataFolder"/>.</param>
    public JsonContext(IOptions<JsonConnection> options)
    {
        _directoryInfo = new DirectoryInfo(options.Value.JsonDataFolder);
        _directoryInfo.Create();
    }

    /// <summary>
    /// Gets a table for entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Keyed entity type.</typeparam>
    /// <returns>File-backed table for the type.</returns>
    public JsonTable<T> GetTable<T>() where T : class, IKeyed, new() => new(EnsureDirectoryExists<T>());

    private string EnsureDirectoryExists<T>()
    {
        var directoryPath = Path.Combine(_directoryInfo.FullName, typeof(T).GetDisplayName());
        var directoryInfo = new DirectoryInfo(directoryPath);
        directoryInfo.Create();
        return directoryPath;
    }
}
