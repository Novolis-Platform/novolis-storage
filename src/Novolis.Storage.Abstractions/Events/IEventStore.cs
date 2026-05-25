namespace Novolis.Storage.Abstractions.Events;

/// <summary>Event store contract: publish events and subscribe to persisted events.</summary>
public interface IEventStore : IAsyncDisposable
{
    /// <param name="worldId">Session ticks (numeric session identity).</param>
    Task PublishAsync(long worldId, IGameEvent @event);

    Task SubscribeAsync(Func<EventEnvelope, Task> handler, CancellationToken ct);
}
