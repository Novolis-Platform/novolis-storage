namespace Novolis.Storage.Abstractions.Events;

/// <summary>
/// Player intent (request). Commands are submitted by clients and processed by the sim;
/// the sim produces events (facts). Concrete command types live in the Domain project.
/// </summary>
public interface IGameCommand
{
    Guid PlayerId { get; }
    long ClientTick { get; }
}
