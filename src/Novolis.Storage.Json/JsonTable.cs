using System.Text.Json;

namespace Novolis.Storage.Json;

/// <summary>
/// Thread-safe JSON file table for a single entity type.
/// </summary>
/// <typeparam name="T">Entity type serialized to disk.</typeparam>
public class JsonTable<T> where T : class, new()
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1);
    private readonly string _folderPath;

    /// <summary>
    /// Creates a table rooted at the given folder path.
    /// </summary>
    /// <param name="folderPath">Directory containing <c>{id}.json</c> files.</param>
    public JsonTable(string folderPath) => _folderPath = folderPath;

    private static string GetFilePath(string folderPath, Guid id) => Path.Combine(folderPath, $"{id}.json");

    /// <summary>
    /// Loads an entity by identifier.
    /// </summary>
    /// <param name="id">Entity key.</param>
    /// <returns>Deserialized entity, or <see langword="null"/> when the file is missing.</returns>
    public async Task<T?> GetAsync(Guid id)
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            var filePath = GetFilePath(_folderPath, id);
            if (!File.Exists(filePath))
                return null;
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Writes an entity to disk, replacing any existing file with the same id.
    /// </summary>
    /// <param name="id">Entity key used for the file name.</param>
    /// <param name="entity">Entity to serialize.</param>
    public async Task SaveAsync(Guid id, T entity)
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            var filePath = GetFilePath(_folderPath, id);
            var json = JsonSerializer.Serialize(entity);
            await File.WriteAllTextAsync(filePath, json);
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Enumerates all JSON files in the table directory.
    /// </summary>
    /// <returns>Deserialized entities (may include null entries on parse failure).</returns>
    public async IAsyncEnumerable<T?> GetAllAsync()
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(_folderPath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                yield return JsonSerializer.Deserialize<T>(json);
            }
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Deletes the JSON file for the given id when it exists.
    /// </summary>
    /// <param name="id">Entity key.</param>
    public async Task DeleteAsync(Guid id)
    {
        await _semaphoreSlim.WaitAsync();
        try
        {
            File.Delete(GetFilePath(_folderPath, id));
        }
        finally
        {
            _semaphoreSlim.Release();
        }
    }
}
