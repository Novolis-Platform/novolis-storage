# Novolis.Storage.Abstractions

Shared repository contracts and DI helpers for Novolis storage backends.

## Install

```bash
dotnet add package Novolis.Storage.Abstractions
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`).

## Quick start

```csharp
services.AddDataStorageRepository<MyRepository, MyEntity>();
```

Entities implement `IKeyed` with a `Guid Id`.

## Related packages

| Package | When to use |
|---------|-------------|
| `Novolis.Storage.Json` | File-based JSON persistence |
| `Novolis.Storage.Sqlite` | SQLite persistence |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-storage/blob/main/docs/design.md)

## Support

Pre-release; repository SQL is generated from entity property reflection.
