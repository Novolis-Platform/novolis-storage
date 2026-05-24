# Novolis.Storage.Sqlite

SQLite-backed `IRepository<T>` with automatic table creation.

## Install

```bash
dotnet add package Novolis.Storage.Sqlite
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`).

## Quick start

```csharp
services.AddSqliteDataStorage<MyEntity>(configuration);
```

Configure `ConnectionStrings:SqliteConnection` or use the default `SqliteData/Storage.db` path.

## Related packages

| Package | When to use |
|---------|-------------|
| `Novolis.Storage.Abstractions` | Repository contracts |
| `Novolis.Storage.Json` | File-based storage without SQLite |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/design.md)

## Support

Pre-release; schema mapping uses reflection over entity properties.
