namespace Novolis.Storage.Abstractions.Events;

/// <summary>Optional snapshot compaction for long-running sessions.</summary>
public interface ISnapshotCapableEventStore
{
    void SnapshotWorld(long worldId, object worldState);
}
