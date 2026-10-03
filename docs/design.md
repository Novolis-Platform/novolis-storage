# Design

The longer proposed architecture is documented in
[`docs/spec/new-storage-library.md`](spec/new-storage-library.md).

## Packages

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | `IHasId`, `IRepository<T>`, DI registration helpers |
| `Novolis.Storage.Query` | Immutable finite query intent, capability discovery, and continuation |
| `Novolis.Storage.Indexing` | Experimental revision-aware key encoding and query planning |
| `Novolis.Storage.AzureCombinedStorage` | Preview aggregate storage over Azure Tables and Blobs |
| `Novolis.Storage.AzureTables` | Azure Table rows; `Id` is the row key and queries are paged OData filters |
| `Novolis.Storage.Json` | One JSON file per entity under a type folder |
| `Novolis.Storage.Sqlite` | SQLite tables created from entity shape |

## Entity model

All stored types implement `IHasId` with a `Guid Id`. Display names for folders/tables come from the provider's type naming conventions.

## Trade-offs

JSON storage is simple and portable; SQLite adds querying and relational constraints with generated SQL from property reflection.
