# Novolis.Storage.Json

File-based JSON repository implementation for `IKeyed` entities.

## Install

```bash
dotnet add package Novolis.Storage.Json
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`).

## Quick start

```csharp
services.AddJsonDataStorage<MyEntity>(configuration);
```

Configure `ConnectionStrings:JsonConnection` or rely on the default folder under the app base directory.

## Related packages

| Package | When to use |
|---------|-------------|
| `Novolis.Storage.Abstractions` | Required contracts (`IRepository<T>`, `IKeyed`) |
| `Novolis.Storage.Sqlite` | SQLite instead of JSON files |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/design.md)

## Support

Pre-release; one JSON file per entity id under a type-named subfolder.
