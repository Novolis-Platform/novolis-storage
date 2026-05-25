namespace Novolis.Storage.Abstractions.Events;

/// <summary>Optional snapshot compaction for long-running streams.</summary>
public interface ISnapshotCapableEventStore
{
    /// <summary>Writes a materialized state snapshot for the stream.</summary>
    void WriteSnapshot(StreamId streamId, object state);
}
