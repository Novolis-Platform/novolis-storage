# Novolis.Storage.Sqlite

SQLite-backed `IRepository<T>` with automatic table creation from entity properties via reflection.

## Install

```bash
dotnet add package Novolis.Storage.Sqlite
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`).

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Sqlite;

services.AddSqliteDataStorage<MyEntity>(configuration);
```

Entity type must implement `IKeyed`. Configure `ConnectionStrings:SqliteConnection` or use the default `SqliteData/Storage.db` path.

```csharp
var repo = sp.GetRequiredService<IRepository<MyEntity>>();
await repo.UpsertAsync(entity, cancellationToken);
```

## API

| Type | Role |
|------|------|
| `ServiceCollectionExtensions.AddSqliteDataStorage<T>` | Registers `ISqliteClient`, options, `SqliteRepository<T>` |
| `IRepository<T>` | From Abstractions — CRUD contract |
| `IKeyed` | Required entity marker for SQLite storage |

Schema mapping uses reflection over entity properties (pre-release).

## Related

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | Repository contracts and `AddStorage` |
| `Novolis.Storage.Json` | File-based storage without SQLite |
| `Novolis.Storage.LiteDb` | Embedded document database alternative |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/design.md)
