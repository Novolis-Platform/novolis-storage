using System.Text.Json;

namespace Novolis.Storage.Ndjson;

/// <summary>Behavioral options for a typed NDJSON store.</summary>
public sealed class NdjsonStoreOptions
{
    /// <summary>Creates options with the supplied serializer configuration.</summary>
    public NdjsonStoreOptions(JsonSerializerOptions? jsonSerializerOptions = null) =>
        JsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

    /// <summary>Serializer options used for appended and replaced values.</summary>
    public JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>Buffer size used for sequential store reads and replacement writes.</summary>
    public int BufferSize { get; set; } = 64 * 1024;

    /// <summary>
    /// When true, malformed or rejected physical records are skipped so later records remain readable.
    /// </summary>
    public bool SkipMalformedRecords { get; set; } = true;

    internal void Validate()
    {
        if (BufferSize < 256)
            throw new ArgumentOutOfRangeException(nameof(BufferSize), "The buffer size must be at least 256 bytes.");
    }
}
