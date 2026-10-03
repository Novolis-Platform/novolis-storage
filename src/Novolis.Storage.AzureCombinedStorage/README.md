# Novolis.Storage.AzureCombinedStorage

Preview aggregate storage over Azure Table Storage and Azure Blob Storage.

The manifest is the publication point for an immutable aggregate revision.
Blob state and indexes are derived physical representations; application
entities remain free of Azure persistence types.

## Install

```bash
dotnet add package Novolis.Storage.AzureCombinedStorage
```

## Quick start

```csharp
services.AddStorage(builder => builder.AddAzureCombinedStorage(options =>
{
    options.ConnectionString = connectionString;
    options.AddIndex<Invoice>("organization-status", invoice => invoice.Status);
}));

var invoices = await repository
    .Query()
    .Where(invoice => invoice.Status).Is(InvoiceStatus.Approved)
    .Take(100)
    .ToListAsync(cancellationToken);
```

The provider publishes blobs and revision-scoped index entries before
conditionally publishing the manifest. Stale candidates are rejected by
manifest revision validation. Use `IAzureCombinedMaintenance<T>` for index
rebuilds, consistency verification, stale-entry removal, and orphan cleanup.
