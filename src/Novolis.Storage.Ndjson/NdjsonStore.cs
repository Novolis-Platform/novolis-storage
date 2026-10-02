using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Novolis.IO.Ndjson;

namespace Novolis.Storage.Ndjson;

/// <summary>
/// Typed append-oriented NDJSON storage with atomic replacement and tolerant sequential reads.
/// </summary>
/// <remarks>
/// The store coordinates operations made through one instance. It deliberately does not take
/// an operating-system file lock; callers sharing a path across processes or pods must assign
/// one writer owner.
/// </remarks>
public sealed class NdjsonStore : IDisposable
{
    private readonly NdjsonStoreOptions _options;
    private readonly NdjsonFileWriter _writer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;

    /// <summary>Creates a store at <paramref name="path"/>.</summary>
    public NdjsonStore(string path, NdjsonStoreOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _options = options ?? new NdjsonStoreOptions();
        _options.Validate();
        FilePath = System.IO.Path.GetFullPath(path);
        _writer = new NdjsonFileWriter(FilePath, _options.JsonSerializerOptions);
    }

    /// <summary>Absolute path of the physical NDJSON file.</summary>
    public string FilePath { get; }

    /// <summary>Appends one value and optionally flushes it to the storage device.</summary>
    public void Append<T>(T value, bool flushToDisk = false)
    {
        Enter();
        try
        {
            _writer.Append(value, flushToDisk);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Appends an already serialized UTF-8 JSON value.</summary>
    public void AppendJson(ReadOnlySpan<byte> json, bool flushToDisk = false)
    {
        Enter();
        try
        {
            _writer.AppendJson(json, flushToDisk);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Appends one value asynchronously.</summary>
    public async ValueTask AppendAsync<T>(
        T value,
        CancellationToken cancellationToken = default,
        bool flushToDisk = false)
    {
        await WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.AppendAsync(value, cancellationToken, flushToDisk).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Appends an already serialized UTF-8 JSON value asynchronously.</summary>
    public async ValueTask AppendJsonAsync(
        ReadOnlyMemory<byte> json,
        CancellationToken cancellationToken = default,
        bool flushToDisk = false)
    {
        await WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.AppendJsonAsync(json, cancellationToken, flushToDisk).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Atomically replaces the file with one serialized value per line.
    /// </summary>
    public async ValueTask ReplaceAsync<T>(
        IEnumerable<T> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        await WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var directory = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using (var stream = new FileStream(
                temporaryPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    BufferSize = _options.BufferSize,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                }))
            await using (var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                _options.BufferSize))
            {
                foreach (var value in values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteLineAsync(
                        JsonSerializer.Serialize(value, _options.JsonSerializerOptions).AsMemory(),
                        cancellationToken).ConfigureAwait(false);
                }

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _gate.Release();
        }
    }

    /// <summary>
    /// Reads complete records in physical order. Malformed records are skipped by default.
    /// </summary>
    public async IAsyncEnumerable<T> ReadAsync<T>(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!File.Exists(FilePath))
            yield break;

        await using var stream = new FileStream(
            FilePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = _options.BufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: _options.BufferSize);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(line, _options.JsonSerializerOptions);
            }
            catch (JsonException) when (_options.SkipMalformedRecords)
            {
                continue;
            }
            catch (ArgumentException) when (_options.SkipMalformedRecords)
            {
                continue;
            }
            catch (NotSupportedException) when (_options.SkipMalformedRecords)
            {
                continue;
            }

            if (value is not null)
                yield return value;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _writer.Dispose();
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    private void Enter()
    {
        ThrowIfDisposed();
        _gate.Wait();
        try
        {
            ThrowIfDisposed();
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
