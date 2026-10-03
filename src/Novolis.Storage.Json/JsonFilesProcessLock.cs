namespace Novolis.Storage.Json;

/// <summary>Container-owned wrapper that releases the JSON store's process lock when the host is disposed.</summary>
internal sealed class JsonFilesProcessLock : IDisposable
{
    private readonly IDisposable handle;

    /// <summary>Initializes the lock wrapper.</summary>
    public JsonFilesProcessLock(IDisposable handle)
    {
        this.handle = handle ?? throw new ArgumentNullException(nameof(handle));
    }

    /// <inheritdoc />
    public void Dispose() => handle.Dispose();
}
