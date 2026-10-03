using System.Linq.Expressions;
using Azure.Core;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Indexing;
using Novolis.Storage.Query;

namespace Novolis.Storage.AzureCombinedStorage;

/// <summary>Configuration for the preview Azure Table and Blob aggregate provider.</summary>
public sealed class AzureCombinedStorageOptions
{
    private readonly Dictionary<Type, List<IndexDescriptor>> _indexes = [];

    /// <summary>Development/test connection string for the Azure-compatible services.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Table endpoint used with <see cref="Credential"/>.</summary>
    public Uri? TableServiceUri { get; set; }

    /// <summary>Blob endpoint used with <see cref="Credential"/>.</summary>
    public Uri? BlobServiceUri { get; set; }

    /// <summary>Managed identity or other token credential for production endpoints.</summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>Optional prefix used to isolate tables and containers.</summary>
    public string? TablePrefix { get; set; }

    /// <summary>Blob container name used for immutable aggregate revisions.</summary>
    public string BlobContainerName { get; set; } = "novolis-combined";

    /// <summary>Number of deterministic primary manifest shards.</summary>
    public int ShardCount { get; set; } = 256;

    /// <summary>Maximum service page size used by manifest and index reads.</summary>
    public int MaxPerPage { get; set; } = 100;

    /// <summary>Maximum number of concurrent blob reads during hydration.</summary>
    public int MaxParallelBlobReads { get; set; } = 8;

    /// <summary>Logical schema version written into each manifest and blob.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Adds an experimental composite index definition for <typeparamref name="T"/>.</summary>
    public void AddIndex<T>(
        string identity,
        params Expression<Func<T, object?>>[] selectors)
        where T : class, IHasId
    {
        ArgumentNullException.ThrowIfNull(selectors);
        if (selectors.Length == 0)
            throw new ArgumentException("An index requires at least one selector.", nameof(selectors));

        var paths = selectors
            .Select(PropertyPathParser.Parse)
            .ToArray();
        var descriptor = new IndexDescriptor(identity, paths);
        if (!_indexes.TryGetValue(typeof(T), out var entries))
        {
            entries = [];
            _indexes.Add(typeof(T), entries);
        }

        entries.RemoveAll(existing => string.Equals(existing.Identity, identity, StringComparison.Ordinal));
        entries.Add(descriptor);
    }

    internal IReadOnlyList<IndexDescriptor> GetIndexes(Type entityType)
    {
        if (_indexes.TryGetValue(entityType, out var configured))
            return configured;

        return IndexPathCatalog.Discover(entityType)
            .Select(path => new IndexDescriptor(path.Value, [path]))
            .ToArray();
    }
}
