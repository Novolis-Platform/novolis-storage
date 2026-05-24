# Design

## Packages

| Package | Role |
|---------|------|
| `Novolis.Storage.Abstractions` | `IKeyed`, `IRepository<T>`, DI registration helpers |
| `Novolis.Storage.Json` | One JSON file per entity under a type folder |
| `Novolis.Storage.Sqlite` | SQLite tables created from entity shape |

## Entity model

All stored types implement `IKeyed` with a `Guid Id`. Display names for folders/tables come from `TypeNameExtensions.GetDisplayName`.

## Trade-offs

JSON storage is simple and portable; SQLite adds querying and relational constraints with generated SQL from property reflection.
