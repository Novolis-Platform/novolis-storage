using Microsoft.Extensions.Logging;

namespace Novolis.Storage.Abstractions.Events;

/// <summary>
/// Marker and apply contract for game events. Implementations cast world to the app's World type.
/// </summary>
public interface IGameEvent
{
    /// <summary>
    /// Apply this event to the given world state. <paramref name="world"/> is the app's domain world type (cast as needed).
    /// </summary>
    void ApplyTo(object world, ILogger logger);
}
