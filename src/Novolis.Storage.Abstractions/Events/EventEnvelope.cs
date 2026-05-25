namespace Novolis.Storage.Abstractions.Events;

/// <summary>Persisted event entry with stream partition and UTC timestamp.</summary>
/// <param name="StreamId">Ordered log partition.</param>
/// <param name="Payload">Application-defined payload (serialize in the provider).</param>
/// <param name="TimestampUtc">When the entry was recorded.</param>
public record EventEnvelope(StreamId StreamId, object Payload, DateTime TimestampUtc);
