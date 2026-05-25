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

Game-specific command/event apply logic belongs in product repos (`novolis-gaming`, SCR), not here.

## Install

```bash
dotnet add package Novolis.Storage.Abstractions
```
