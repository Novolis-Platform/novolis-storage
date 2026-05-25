# Novolis.Storage.Abstractions

Repository and event-journal contracts with `AddStorage` DI composition.

## Event journal (generic)

| Type | Role |
|------|------|
| `StreamId` | Partition key for an ordered log |
| `SessionId` | Sortable session key (maps to `StreamId` via ticks) |
| `EventEnvelope` | `StreamId` + opaque `Payload` + `TimestampUtc` |
| `IEventStore` | Append and subscribe |
| `IReadableEventStore` | List events for a stream |
| `ISnapshotCapableEventStore` | Optional snapshot compaction |

Game-specific command/event apply logic belongs in product repos, not here.

## Install

```bash
dotnet add package Novolis.Storage.Abstractions
```

## Quick start

```csharp
services.AddStorageAbstractions();
var stream = StreamId.FromSession(SessionId.New());
await eventStore.PublishAsync(stream, new { Kind = "joined" }, cancellationToken);
```
