namespace Novolis.Storage.Abstractions.Events;

/// <summary>Wrapper for a game event with world id (session ticks) and timestamp.</summary>
public record EventEnvelope(long WorldId, IGameEvent Event, DateTime Timestamp);
