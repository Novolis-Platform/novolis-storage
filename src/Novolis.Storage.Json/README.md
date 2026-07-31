# Novolis.Storage.Json

JSON file-per-entity `IRepository<T>` provider. Layout: `{RootPath}/{TypeName}/{Id:N}.json`. Uses `Novolis.IO.Workspace` for reads/writes and optional cross-process locking.

## Install

```bash
dotnet add package Novolis.Storage.Json
```

Depends on `Novolis.Storage.Abstractions` and `Novolis.IO.Workspace`.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.Json;

services.AddStorage(builder => builder.AddJsonProvider(o =>
{
    o.RootPath = @"C:\data\saves";
    o.UseProcessLock = true;
    o.CreateIfMissing = true;
}));

var repo = sp.GetRequiredService<IRepository<MyEntity>>();
await repo.UpsertAsync(entity, cancellationToken);
```

`AddJsonFilesProvider` is an alias for `AddJsonProvider`.

Create with a new id:

```csharp
var entity = await repo.CreateAsync(
    idProvider,
    id => new MyEntity { Id = id },
    cancellationToken);
```

Startup validates root existence, read/write probes, and optional `.store.lock` when `UseProcessLock && !ReadOnly`.

## API

| Type | Role |
|------|------|
| `JsonFilesOptions` | `RootPath`, `ReadOnly`, `CreateIfMissing`, `UseProcessLock`, `LockStripes`, `JsonSerializerOptions` |
| `JsonStorageWorkspaceKey` | Keyed DI name for the backing `IFileWorkspace` (`Default`) |
| `JsonStorageExtensions.AddJsonProvider` | Registers workspace, options, lock, repositories |
| `RepositoryCreateExtensions.CreateAsync` | Factory helper on `IRepository<T>` |

## Related

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | `IRepository<T>`, `AddStorage`, `IIdProvider` |
| `Novolis.IO.Workspace` | `PhysicalFileWorkspace` used internally |
| `Novolis.Storage.LiteDb` | Single-file document store alternative |
