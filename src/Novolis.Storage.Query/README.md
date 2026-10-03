# Novolis.Storage.Query

Provider-independent, finite query contracts for Novolis repositories.

The package keeps query intent separate from physical storage. Providers opt in
through `IRepositoryQueryProvider<T>` and reject unsupported or unbounded plans
explicitly.

## Install

```bash
dotnet add package Novolis.Storage.Query
```

## Quick start

```csharp
var page = await repository
    .Query()
    .Where(entity => entity.Id).Is(id)
    .Take(100)
    .ToPageAsync(cancellationToken);
```
