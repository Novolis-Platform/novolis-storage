namespace Novolis.Storage.Abstractions;

/// <summary>Unique session key: base62 encoding of UTC creation ticks.</summary>
public readonly record struct SessionId(string Base62, long Ticks)
{
    public static SessionId CreateNow() => FromTicks(DateTime.UtcNow.Ticks);

    public static SessionId FromTicks(long ticks) => new(SortableBase62.Encode(ticks), ticks);

    public static SessionId Parse(string base62) => new(base62, SortableBase62.Decode(base62));

    public override string ToString() => Base62;
}
