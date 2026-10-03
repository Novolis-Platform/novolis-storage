# Getting started

Novolis storage provides a shared `IRepository<T>` abstraction with JSON,
SQLite, in-memory, direct Azure Tables, and preview Azure Combined Storage
implementations. Querying is an optional finite capability.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build

```bash
dotnet build Novolis.Storage.slnx
```

## JSON storage

```csharp
services.AddJsonDataStorage<MyEntity>(configuration);
```

## SQLite storage

```csharp
services.AddSqliteDataStorage<MyEntity>(configuration, databaseName: "app.db");
```

## Azure Combined Storage

```csharp
services.AddStorage(builder => builder.AddAzureCombinedStorage(options =>
{
    options.ConnectionString = connectionString;
    options.AddIndex<MyEntity>("name", entity => entity.Name);
}));

var repository = serviceProvider.GetRequiredService<IRepository<MyEntity>>();
var page = await repository
    .Query()
    .Where(entity => entity.Name).StartsWith("Ada")
    .Take(100)
    .ToPageAsync(cancellationToken);
```

The provider stores the published manifest in Azure Tables and immutable
aggregate payloads in Azure Blobs. Indexes are derived state and can be
rebuilt or repaired through `IAzureCombinedMaintenance<T>`.

## See also

- [Design](design.md)
- [Release](release.md)
