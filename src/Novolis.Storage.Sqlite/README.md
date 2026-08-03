<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-storage">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Storage.Sqlite

SQLite-backed `IRepository<T>` provider for Novolis storage abstractions. Registers a shared `ISqliteClient` singleton plus open-generic repositories.

## Install

```bash
dotnet add package Novolis.Storage.Sqlite
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`). Depends on **Microsoft.Data.Sqlite** and `Novolis.Storage.Abstractions`.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Sqlite;

services.AddStorage(builder => builder.AddSqliteProvider(o =>
{
    o.ConnectionString = "Data Source=Data/novolis.db";
}));

var repo = sp.GetRequiredService<IRepository<MyEntity>>();
await repo.UpsertAsync(entity, cancellationToken);
```

Entity types must implement `IHasId`. Use `Data Source=:memory:` for an in-process database (shared connection for the app lifetime).

## API

| Type | Role |
|------|------|
| `SqliteOptions` | `ConnectionString` |
| `SqliteStorageExtensions.AddSqliteProvider` | Registers `ISqliteClient`, `IRepositoryProvider`, `IRepository<>` |
| `ISqliteClient` | Low-level connection access for custom SQL |

`AddStorage` (from Abstractions) also registers `IIdProvider` → `GuidV7IdProvider` and `IRepositoryFactory`.

## Related

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | `IRepository<T>`, `AddStorage`, `IHasId` |
| `Novolis.Storage.LiteDb` | Embedded document database alternative |
| `Novolis.Storage.Json` | File-based storage without SQLite |
| `Novolis.Storage.InMemory` | Volatile repositories for tests |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/design.md)

