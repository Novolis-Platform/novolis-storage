# Novolis.Storage.InMemory

In-memory `IRepository<T>` provider for unit tests, prototypes, and single-process playtest profiles. Data lives in a process-wide `ConcurrentDictionary` per entity type.

## Install

```bash
dotnet add package Novolis.Storage.InMemory
```

Depends on `Novolis.Storage.Abstractions`. Does **not** implement `IEventStore` — repositories only.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.InMemory;

services.AddStorage(builder => builder.AddInMemoryProvider());

var repo = sp.GetRequiredService<IRepository<MyEntity>>();
await repo.UpsertAsync(entity, cancellationToken);
var all = repo.All();
```

## API

| Type | Role |
|------|------|
| `IInMemoryStore` | Per-type dictionary access via `GetOrAddDictionary<T>()` |
| `InMemoryStorageExtensions.AddInMemoryProvider` | Registers store, `IRepositoryProvider`, `IRepository<>` |

## Related

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | Repository contracts and `AddStorage` |
| `Novolis.Storage.Json` | Durable file-backed repositories |
| `Novolis.IO.Workspace.Testing` | In-memory file workspace (not repositories) |
