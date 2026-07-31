# Novolis.IO.Workspace

Root-scoped file workspace: `IFileProvider` reads plus explicit write, delete, and enumeration helpers. Used by JSON storage and other file-backed Novolis packages.

## Install

```bash
dotnet add package Novolis.IO.Workspace
```

No DI extensions — construct `PhysicalFileWorkspace` directly or register keyed instances in your host.

## Quick start

```csharp
using Novolis.IO.Workspace;

IFileWorkspace workspace = new PhysicalFileWorkspace(rootPath);
await workspace.WriteAllTextAsync("saves/slot1.json", json, cancellationToken);

if (workspace.FileExists("saves/slot1.json"))
{
    var text = await workspace.ReadAllTextAsync("saves/slot1.json", cancellationToken);
}
```

Static disk helpers (no workspace instance): `PhysicalFileWorkspace.FileExistsOnDisk`, `ReadAllTextOnDisk`, `EnumerateFiles`, etc.

## API

| Type | Role |
|------|------|
| `IFileWorkspace` | `Provider`, `RootPath`, read/write/delete, directory helpers |
| `PhysicalFileWorkspace` | Disk-backed implementation; creates root on construction |
| `FileWorkspaceKeys` | Keyed DI name constants (`Storage`, `JsonFileEvents`, `JsonFilesStore`) |

## Related

| Package | Role |
|---------|------|
| `Novolis.IO.Workspace.Testing` | In-memory `IFileWorkspace` for unit tests |
| `Novolis.Storage.Json` | JSON repositories backed by `PhysicalFileWorkspace` |
