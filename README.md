<!-- novolis-package-index:start -->
> **GitHub Packages shows this repository README on every package page** (upstream limitation).
> Open the **package README** for install and quick start — embedded in each .nupkg and linked below.

## Published packages

| Package | Install | Package README |
|---------|---------|----------------|
| `Novolis.Storage.Abstractions` | `dotnet add package Novolis.Storage.Abstractions` | [README](https://github.com/Novolis-Platform/novolis-storage/blob/main/src/Novolis.Storage.Abstractions/README.md) |
| `Novolis.Storage.Json` | `dotnet add package Novolis.Storage.Json` | [README](https://github.com/Novolis-Platform/novolis-storage/blob/main/src/Novolis.Storage.Json/README.md) |
| `Novolis.Storage.LiteDb` | `dotnet add package Novolis.Storage.LiteDb` | [README](https://github.com/Novolis-Platform/novolis-storage/blob/main/src/Novolis.Storage.LiteDb/README.md) |
| `Novolis.Storage.InMemory` | `dotnet add package Novolis.Storage.InMemory` | [README](https://github.com/Novolis-Platform/novolis-storage/blob/main/src/Novolis.Storage.InMemory/README.md) |
| `Novolis.Storage.Sqlite` | `dotnet add package Novolis.Storage.Sqlite` | [README](https://github.com/Novolis-Platform/novolis-storage/blob/main/src/Novolis.Storage.Sqlite/README.md) |
| `Novolis.IO.Workspace` | `dotnet add package Novolis.IO.Workspace` | [README](https://github.com/Novolis-Platform/novolis-storage/blob/main/src/Novolis.IO.Workspace/README.md) |
| `Novolis.IO.Workspace.Testing` | `dotnet add package Novolis.IO.Workspace.Testing` | [README](https://github.com/Novolis-Platform/novolis-storage/blob/main/src/Novolis.IO.Workspace.Testing/README.md) |

For NuGet.org and Visual Studio, the **embedded** README.md inside each package is authoritative.

<!-- novolis-package-index:end -->

# Storage

Repository and event-journal abstractions with pluggable providers (JSON files, LiteDB, in-memory, SQLite) plus root-scoped file workspace helpers.

## Packages

| Package | Description |
|---------|-------------|
| `Novolis.Storage.Abstractions` | `IRepository<T>`, event journal contracts, `AddStorage` |
| `Novolis.Storage.Json` | File-per-entity JSON repositories |
| `Novolis.Storage.LiteDb` | LiteDB document store |
| `Novolis.Storage.InMemory` | In-memory repositories for tests |
| `Novolis.Storage.Sqlite` | SQLite-backed repositories |
| `Novolis.IO.Workspace` | Root-scoped file workspace (`IFileWorkspace`) |
| `Novolis.IO.Workspace.Testing` | In-memory workspace for unit tests |

## Quick start

```csharp
services.AddStorage(builder => builder.AddJsonProvider(o => o.RootPath = root));
var repo = sp.GetRequiredService<IRepository<MyEntity>>();
```

## Documentation

- [Getting started](docs/getting-started.md)
- [Design](docs/design.md)
- [Release](docs/release.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Security

See [SECURITY.md](SECURITY.md).
