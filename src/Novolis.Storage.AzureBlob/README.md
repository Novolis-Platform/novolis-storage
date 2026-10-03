<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-storage/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-storage/) · [Source](https://github.com/Novolis-Platform/novolis-storage)
<!-- novolis-pkg-brand:end -->

# Novolis.Storage.AzureBlob

Typed JSON blob containers backed directly by Azure Blob Storage. A value's
`IHasName.Name` is its blob name. This package intentionally contains no Query
or Index dependency; it provides only named blob persistence.

## Install

```bash
dotnet add package Novolis.Storage.AzureBlob
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download)
(`net10.0`) and an Azure Blob Storage account or Azurite.

## Quick start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureBlob;

services.AddStorage(builder => builder.AddAzureBlobStorage(options =>
{
    options.ConnectionString = connectionString;
    options.ContainerName = "documents";
}));

var blobs = serviceProvider.GetRequiredService<IBlobContainer<Document>>();
await blobs.UpsertAsync(new Document { Name = "welcome.json", Text = "Hello." });

var document = await blobs.TryGetAsync("welcome.json", cancellationToken);
await foreach (var item in blobs.ListAsync(cancellationToken))
{
}
```

Values are serialized with `System.Text.Json`. Configure
`AzureBlobStorageOptions.SerializerOptions` when a different JSON policy is
needed. `ContainerName` must be a valid Azure container name, while blob names
may use Azure's normal hierarchical naming conventions.

```csharp
public sealed class Document : IHasName
{
    public string Name { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}
```

## API

| Type | Role |
|------|------|
| `IHasName` | Stable name contract from `Novolis.Storage.Abstractions` |
| `IBlobContainer<T>` | Async list, read, upsert, and delete operations |
| `AzureBlobStorageOptions` | Connection, endpoint, container, paging, and JSON settings |
| `AzureBlobStorageExtensions.AddAzureBlobStorage` | Registers Azure Blob clients and typed containers |

The provider creates the configured container on first use, replaces blobs on
upsert, returns `null` for missing reads, and returns `false` when a delete
finds no blob.
