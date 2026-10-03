namespace Novolis.Storage.Query;

/// <summary>Validated, dot-separated path to a property in an entity.</summary>
public sealed record PropertyPath
{
    public PropertyPath(IReadOnlyList<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0 || segments.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A property path must contain at least one named segment.", nameof(segments));

        Segments = segments.ToArray();
        Value = string.Join('.', Segments);
    }

    public IReadOnlyList<string> Segments { get; }

    public string Value { get; }

    public bool Equals(PropertyPath? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
