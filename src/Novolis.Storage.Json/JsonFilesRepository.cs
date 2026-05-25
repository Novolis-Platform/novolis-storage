using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Novolis.IO.Workspace;
using Novolis.Storage.Abstractions;

namespace Novolis.Storage.Json;

internal sealed class JsonFilesRepository<T> : IRepository<T> where T : class, IHasId
{
    private readonly string _typeDir;
    private readonly IFileWorkspace _workspace;
    private readonly JsonFilesOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim[] _stripes;

    public JsonFilesRepository(
        JsonFilesOptions options,
        [FromKeyedServices(JsonStorageWorkspaceKey.Default)] IFileWorkspace workspace)
    {
        _options = options;
        _workspace = workspace;
        _jsonOptions = options.JsonSerializerOptions;
        _typeDir = Path.Combine(_options.RootPath, typeof(T).Name);
        _stripes = new SemaphoreSlim[Math.Max(1, _options.LockStripes)];
        for (var i = 0; i < _stripes.Length; i++)
            _stripes[i] = new SemaphoreSlim(1, 1);
    }

    private SemaphoreSlim GetStripe(Guid id)
    {
        var idx = Math.Abs(unchecked((typeof(T).FullName?.GetHashCode(StringComparison.Ordinal) ?? 0) + id.GetHashCode())) % _stripes.Length;
        return _stripes[idx];
    }

    private static string IdToFileName(Guid id) => id.ToString("N") + ".json";

    private string GetFilePath(Guid id) => Path.Combine(_typeDir, IdToFileName(id));

    public IEnumerable<T> All()
    {
        if (!_workspace.DirectoryExists(_typeDir))
            yield break;

        foreach (var file in _workspace.EnumerateFiles(_typeDir, "*.json"))
        {
            var entity = TryReadFile(file);
            if (entity != null)
                yield return entity;
        }
    }

    private T? TryReadFile(string path)
    {
        try
        {
            var json = _workspace.ReadAllTextAsync(path).GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async ValueTask<T?> TryGetAsync(Guid id, CancellationToken ct = default)
    {
        var path = GetFilePath(id);
        if (!_workspace.FileExists(path))
            return null;

        try
        {
            await using var stream = _workspace.CreateFileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                FileOptions.Asynchronous);
            return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async ValueTask UpsertAsync(T entity, CancellationToken ct = default)
    {
        if (entity.Id == Guid.Empty)
            throw new ArgumentException("Entity Id must not be empty.", nameof(entity));

        if (_options.ReadOnly)
            throw new InvalidOperationException("JSON files store is configured read-only.");

        var id = entity.Id;
        var sem = GetStripe(id);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetFilePath(id);
            var dir = Path.GetDirectoryName(path)!;
            if (!_workspace.DirectoryExists(dir))
                _workspace.EnsureDirectoryExists(dir);

            var tmp = path + ".tmp";
            await using (var stream = _workspace.CreateFileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096,
                         FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, entity, _jsonOptions, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            _workspace.MoveFile(tmp, path, overwrite: true);
        }
        finally
        {
            sem.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (_options.ReadOnly)
            throw new InvalidOperationException("JSON files store is configured read-only.");

        var sem = GetStripe(id);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = GetFilePath(id);
            if (!_workspace.FileExists(path))
                return false;
            _workspace.DeleteFile(path);
            return true;
        }
        finally
        {
            sem.Release();
        }
    }
}
