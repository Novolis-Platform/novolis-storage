# Novolis.Storage.Indexing

Experimental revision-aware indexing infrastructure for Novolis storage
providers.

Index definitions, key encoding, and physical layouts are provider
implementation details and may evolve before this package becomes a stable
consumer-facing API.

## Install

```bash
dotnet add package Novolis.Storage.Indexing
```

## Quick start

Indexing is normally consumed indirectly by a storage provider. Configure
provider-specific indexes there rather than depending on physical index keys
from application code.
