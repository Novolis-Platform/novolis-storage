namespace Novolis.Storage.Abstractions.Events;

/// <summary>Event stores that can list persisted events for replay or diagnostics.</summary>
public interface IReadableEventStore
{
    /// <summary>All envelopes for <paramref name="worldId"/> (session ticks) in persistence order.</summary>
    IEnumerable<EventEnvelope> GetEventsForWorld(long worldId);
}
