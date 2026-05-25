namespace Novolis.Storage.Abstractions.Events;

/// <summary>Append-only event journal: publish entries and subscribe to persisted envelopes.</summary>
public interface IEventStore : IAsyncDisposable
{
    /// <summary>Appends a payload to the stream partition.</summary>
    Task PublishAsync(StreamId streamId, object payload, CancellationToken cancellationToken = default);

    /// <summary>Invoked for each persisted envelope (implementation defines ordering guarantees).</summary>
    Task SubscribeAsync(Func<EventEnvelope, Task> handler, CancellationToken cancellationToken = default);
}
