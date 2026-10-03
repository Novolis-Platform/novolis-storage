<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-storage/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-storage/) · [Source](https://github.com/Novolis-Platform/novolis-storage)
<!-- novolis-pkg-brand:end -->

# Novolis.Storage.AzureTables

Azure Table Storage `IRepository<T>` provider. `Id` is the row key. Queries are OData filters executed by the service, including every continuation page.

## Install

```bash
dotnet add package Novolis.Storage.AzureTables
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`). Depends on **Azure.Data.Tables** and `Novolis.Storage.Abstractions`.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureTables;

services.AddStorage(builder => builder.AddAzureTableProvider(o =>
{
    o.ConnectionString = connectionString;
}));

var repo = sp.GetRequiredService<IRepository<MyEntity>>();
await repo.UpsertAsync(entity, cancellationToken);

await foreach (var name in repo.Query().Where(e => e.Active).Select(e => e.Name))
{
}
```

Entity types must implement `IHasId` and have a parameterless constructor. Stored properties are public read/write scalars: string, bool, integer, floating point, `Guid`, `DateTime`, `DateTimeOffset`, and enums.

## Queries

`Query()` on `IRepository<T>` returns `IAzureTableQuery<T>` (`IQueryable<T>` and `IAsyncEnumerable<T>`). `Where` and `Select` are translated to OData `filter` and `select` when the operator is applied. `Take` stops the paged stream. `OrderBy`, `Skip`, `Contains`, and `StartsWith` throw `NotSupportedException` — nothing is evaluated on the client.

`TryGetAsync` is a point read on `PartitionKey` + `RowKey`. Queries always include `PartitionKey eq 'row'` and follow continuation tokens. Row keys are strings, not `guid'...'`.

## API

| Type | Role |
|------|------|
| `AzureTableOptions` | `ConnectionString`, `TablePrefix`, `MaxPerPage` |
| `AzureTableStorageExtensions.AddAzureTableProvider` | Registers `TableServiceClient`, `IRepository<>`, `IAzureTableRepository<>` |
| `AzureTableRepositoryExtensions.Query` | `IRepository<T>` extension that starts an `IAzureTableQuery<T>` |
| `IAzureTableQuery<T>` | `Where`, `Select`, and `Take` translated to Table Storage; `await foreach` to read |

`AddStorage` (from Abstractions) also registers `IIdProvider` → `GuidV7IdProvider` and `IRepositoryFactory`.

## Related

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | `IRepository<T>`, `AddStorage`, `IHasId` |
| `Novolis.Storage.Sqlite` | Local relational alternative |
| `Novolis.Storage.InMemory` | Volatile repositories for tests |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/design.md)
