# Novolis.IO.Workspace.Testing

In-memory `IFileWorkspace` for unit tests.

## Install

```bash
dotnet add package Novolis.IO.Workspace.Testing
```

## Quick start

```csharp
var workspace = new InMemoryFileWorkspace();
await workspace.WriteTextAsync("config.json", "{}", CancellationToken.None);
```
