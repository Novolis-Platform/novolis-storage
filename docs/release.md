# Release

Publish the packable storage packages together after the solution build, unit
tests, coverage, and governance checks succeed:

- `Novolis.Storage.Abstractions`
- `Novolis.Storage.AzureBlob`
- `Novolis.Storage.Query`
- `Novolis.Storage.Indexing`
- `Novolis.Storage.AzureCombinedStorage`
- `Novolis.Storage.AzureTables`
- `Novolis.Storage.InMemory`
- `Novolis.Storage.Json`
- `Novolis.Storage.LiteDb`
- `Novolis.Storage.Ndjson`
- `Novolis.Storage.Sqlite`

The `main` workflow publishes Novolis packages to GitHub Packages and then
publishes the same artifacts to NuGet.org. Versions use `build/version.json`.
Each package includes XML documentation and its README from
`src/<PackageId>/README.md`.

The new query/storage packages are gated at 100% line coverage and at least
90% branch coverage. The current focused result is:

| Package | Line | Branch |
| --- | ---: | ---: |
| `Novolis.Storage.AzureBlob` | 100% | 96.6% |
| `Novolis.Storage.Query` | 100% | 93.8% |
| `Novolis.Storage.Indexing` | 100% | 98.8% |
| `Novolis.Storage.AzureCombinedStorage` | 100% | 90.5% |
