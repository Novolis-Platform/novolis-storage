# Novolis.Storage.LiteDb

LiteDB-backed `IRepository<T>` provider for Novolis storage abstractions. Registers a shared `ILiteDatabase` singleton plus open-generic repositories.

## Install

```bash
dotnet add package Novolis.Storage.LiteDb
```

Depends on **LiteDB** and `Novolis.Storage.Abstractions`.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.LiteDb;

services.AddStorage(builder => builder.AddLiteDbProvider(o =>
{
    o.DatabasePath = "Data/novolis.db";
    o.Password = null; // optional AES password
}));

var repo = sp.GetRequiredService<IRepository<MyEntity>>();
await repo.UpsertAsync(entity, cancellationToken);
```

Use `:memory:` for an in-process database (shared connection for the app lifetime).

## API

| Type | Role |
|------|------|
| `LiteDbOptions` | `DatabasePath`, `Password`, `WriteLockStripes` |
| `LiteDbStorageExtensions.AddLiteDbProvider` | Registers `ILiteDatabase`, `IRepositoryProvider`, `IRepository<>` |

`AddStorage` (from Abstractions) also registers `IIdProvider` → `GuidV7IdProvider` and `IRepositoryFactory`.

## Related

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | `IRepository<T>`, `AddStorage`, `IHasId` |
| `Novolis.Storage.InMemory` | Volatile repositories for tests |
| `Novolis.Storage.Json` | File-per-entity JSON repositories |
