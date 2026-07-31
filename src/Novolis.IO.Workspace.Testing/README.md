# Novolis.IO.Workspace.Testing

In-memory `IFileWorkspace` for unit tests. Implements the full workspace contract without disk I/O — suitable for storage and IO tests that need deterministic file layout.

## Install

```bash
dotnet add package Novolis.IO.Workspace.Testing
```

Depends on `Novolis.IO.Workspace`.

## Quick start

```csharp
using Novolis.IO.Workspace.Testing;

var workspace = new InMemoryFileWorkspace(Path.GetTempPath());
await workspace.WriteAllTextAsync("config.json", "{}", CancellationToken.None);

var exists = workspace.FileExists("config.json");
var json = await workspace.ReadAllTextAsync("config.json", CancellationToken.None);
```

Constructor requires a `rootPath` (logical root for relative paths).

## API

| Type | Role |
|------|------|
| `InMemoryFileWorkspace` | Volatile `IFileWorkspace` + `IFileProvider` implementation |

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Workspace` | Production disk-backed workspace |
| `Novolis.Storage.InMemory` | In-memory repositories (separate from file workspace) |
