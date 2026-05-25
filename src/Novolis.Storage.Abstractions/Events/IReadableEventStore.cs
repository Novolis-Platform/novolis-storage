namespace Novolis.Storage.Abstractions.Events;

/// <summary>Event journals that support listing persisted entries for replay or diagnostics.</summary>
public interface IReadableEventStore
{
    /// <summary>All envelopes for <paramref name="streamId"/> in persistence order.</summary>
    IEnumerable<EventEnvelope> GetEvents(StreamId streamId);
}
