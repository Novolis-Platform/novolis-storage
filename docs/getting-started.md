# Getting started

Novolis storage provides a shared `IRepository<T>` abstraction with JSON file and SQLite implementations.

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

## See also

- [Design](design.md)
- [Release](release.md)
