using Novolis.Storage.Query;

namespace Novolis.Storage.Indexing;

/// <summary>
/// Experimental logical identity for a provider-managed index.
/// </summary>
public sealed record IndexDescriptor
{
    public IndexDescriptor(string identity, IEnumerable<PropertyPath> properties, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException("An index identity is required.", nameof(identity));
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Identity = identity;
        Properties = properties?.ToArray() ?? throw new ArgumentNullException(nameof(properties));
        if (Properties.Count == 0)
            throw new ArgumentException("An index requires at least one property.", nameof(properties));
        Version = version;
    }

    public string Identity { get; }

    public IReadOnlyList<PropertyPath> Properties { get; }

    public int Version { get; }
}
