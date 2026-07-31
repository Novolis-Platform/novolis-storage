# Novolis.Storage.Abstractions

Entity repository and event-journal contracts with `AddStorage` DI composition. Game-specific command/event apply logic belongs in product repos, not here.

## Install

```bash
dotnet add package Novolis.Storage.Abstractions
```

## Quick start — repositories

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.InMemory; // or Json, LiteDb, Sqlite

services.AddStorage(builder => builder.AddInMemoryProvider());

var repo = sp.GetRequiredService<IRepository<MyEntity>>();
await repo.UpsertAsync(entity, cancellationToken);
```

Optional per-type provider override (register a concrete `IRepositoryProvider` first):

```csharp
services.AddStorage(builder =>
{
    builder.AddJsonProvider(o => o.RootPath = root);
    builder.BindRepository<SpecialEntity, MyCustomRepositoryProvider>();
});
```

## Quick start — event journal (contracts)

```csharp
var session = SessionId.CreateNow();
var stream = StreamId.FromSession(session);
await eventStore.PublishAsync(stream, new { Kind = "joined" }, cancellationToken);
```

`IEventStore` implementations are not shipped in this package — only the contracts.

## API — repositories

| Type | Role |
|------|------|
| `IHasId` | Entity marker: `Guid Id { get; }` |
| `IRepository<T>` | `All()`, `TryGetAsync`, `UpsertAsync`, `DeleteAsync` |
| `IRepositoryProvider` | Backend hook: `Create<T>()` |
| `IRepositoryFactory` | Resolves `IRepository<T>` from DI |
| `IIdProvider` / `GuidV7IdProvider` | UUID v7 id generation |
| `IStorageBuilder` | Fluent builder with `Services` |
| `StorageServiceCollectionExtensions.AddStorage` | Registers factory + id provider |
| `StorageTypeBindingExtensions.BindRepository` | Per-type provider binding |

## API — event journal

| Type | Role |
|------|------|
| `StreamId` | Partition key for an ordered log |
| `SessionId` | Sortable session key (base62 + ticks): `CreateNow()`, `FromTicks`, `Parse` |
| `EventEnvelope` | `StreamId` + opaque `Payload` + `TimestampUtc` |
| `IEventStore` | Append and subscribe |
| `IReadableEventStore` | List events for a stream |
| `ISnapshotCapableEventStore` | Optional snapshot compaction |

## Related

| Package | Role |
|---------|------|
| `Novolis.Storage.Json` | File-per-entity repositories |
| `Novolis.Storage.LiteDb` | LiteDB repositories |
| `Novolis.Storage.InMemory` | In-memory repositories |
| `Novolis.Storage.Sqlite` | SQLite repositories |
