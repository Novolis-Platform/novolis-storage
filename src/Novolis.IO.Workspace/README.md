# Novolis.IO.Workspace

Root-scoped file workspace (`IFileProvider` reads + explicit writes).

## Install

```bash
dotnet add package Novolis.IO.Workspace
```

## Quick start

```csharp
IFileWorkspace workspace = new PhysicalFileWorkspace(rootPath);
await workspace.WriteTextAsync("saves/slot1.json", json, cancellationToken);
```
