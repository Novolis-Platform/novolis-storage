# Novolis.Storage.Query

Provider-independent, finite query contracts for Novolis repositories.

The package keeps query intent separate from physical storage. Providers opt in
through `IRepositoryQueryProvider<T>` and reject unsupported or unbounded plans
explicitly.
