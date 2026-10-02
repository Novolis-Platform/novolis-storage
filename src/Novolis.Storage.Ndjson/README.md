<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-storage/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-storage/) · [Source](https://github.com/Novolis-Platform/novolis-storage)
<!-- novolis-pkg-brand:end -->

# Novolis.Storage.Ndjson

Typed append-oriented NDJSON storage for local journals, ledgers, and telemetry.

`Novolis.IO.Ndjson` owns physical line access and bounded sparse-index viewing.
This package adds store behavior: typed serialization, tolerant sequential
reads, per-instance write coordination, and atomic whole-file replacement.

```csharp
using Novolis.Storage.Ndjson;

using var store = new NdjsonStore("events.ndjson");
store.Append(new { Id = 1, Message = "ready" });

await foreach (var item in store.ReadAsync<EventRecord>())
    Console.WriteLine(item.Message);
```

The store does not take an operating-system file lock. One process or pod must
own writes for a given path; readers can share the file while it is appended.
