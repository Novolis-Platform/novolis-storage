namespace Novolis.Storage.Abstractions.Events;

/// <summary>Partition key for an ordered event log (session, aggregate, or tenant stream).</summary>
/// <param name="Value">Opaque numeric stream identity.</param>
public readonly record struct StreamId(long Value)
{
    /// <summary>Maps a <see cref="SessionId"/> ticks value to a stream id.</summary>
    public static StreamId FromSession(SessionId session) => new(session.Ticks);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
