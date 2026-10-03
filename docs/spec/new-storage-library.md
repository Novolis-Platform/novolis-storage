# Novolis Storage: Queryable Azure Combined Storage

**Status:** Proposed  
**Target:** .NET 9+  
**Repository:** `Novolis-Platform/novolis-storage`  
**Primary new packages:** `Novolis.Storage.Query`, `Novolis.Storage.Indexing`, `Novolis.Storage.AzureCombinedStorage`

## 1. Summary

Novolis Storage shall support a queryable aggregate storage model built from Azure Table Storage and Azure Blob Storage while preserving the existing small `IRepository<T>` abstraction.

The design intentionally separates four concerns:

1. **Repository semantics** describe persistence of an aggregate.
2. **Query semantics** describe what a caller wants to find.
3. **Physical storage** decides how aggregate state is divided between Table Storage and Blob Storage.
4. **Indexes and projections** provide derived, rebuildable structures optimized for queries.

The intended mental model is:

```text
                       IRepository<T>
                             │
                  ┌──────────┴──────────┐
                  │                     │
              Get / Store           Query<T>
                  │                     │
                  ▼                     ▼
             Manifest             Query planner
                  │                     │
          ┌───────┴───────┐      ┌──────┴──────┐
          ▼               ▼      ▼             ▼
      Table state      Blob state Indexes   Projections
          │               │      │             │
          └───────────────┴──────┴─────────────┘
                          │
                          ▼
                    Logical aggregate
```

The caller works with normal strongly typed .NET objects.

The caller does not need to know whether a property is:

- stored directly in Table Storage,
- serialized into Blob Storage,
- duplicated into an index,
- represented in a compound key,
- copied into a materialized projection,
- or retrieved through a combination of these mechanisms.

Azure Table Storage becomes primarily the **identity, manifest, indexing, and query-routing layer**.

Azure Blob Storage becomes primarily the **rich aggregate-state layer**.

Indexes and projections are **derived data**, never authoritative state.

This deliberately combines several established Microsoft patterns:

- Large Entities
- Intra-partition Secondary Index
- Inter-partition Secondary Index
- Index Entities
- Compound Keys
- Denormalization
- Query Projection
- Materialized View
- Eventually Consistent Transactions

Microsoft explicitly recommends Blob Storage for large Table entity properties, application-maintained index structures where secondary indexes are unavailable, denormalization and compound keys for read efficiency, and rebuildable materialized views for read models that do not match the authoritative storage shape.

The Novolis design packages those ideas behind a strongly typed repository/query abstraction.

---

# 2. Goals

The implementation shall prioritize the following goals.

### 2.1 Preserve a small repository abstraction

`IRepository<T>` must remain usable by storage implementations that know nothing about querying.

A JSON repository should not acquire a query dependency because an Azure provider supports indexing.

A provider shall opt into capabilities instead.

---

### 2.2 Strongly typed aggregates

Applications persist ordinary strongly typed objects.

At minimum:

```csharp
public interface IHasId
{
    Guid Id { get; }
}
```

A repository for `T` requires:

```csharp
where T : IHasId
```

The physical persistence model must not infect the domain model.

No Azure attributes, blob URLs, `PartitionKey`, `RowKey`, `TableEntity`, or persistence DTOs are required on application types.

---

### 2.3 Storage shape independent from domain shape

A domain model such as:

```csharp
public sealed record Invoice(
    Guid Id,
    InvoiceMetadata Metadata,
    InvoicePayload Payload,
    IReadOnlyList<Attachment> Attachments) : IHasId;
```

may physically resemble:

```text
Manifest
    Id
    Revision
    Metadata
    PayloadBlobRef
    AttachmentsBlobRef

Blob
    InvoicePayload

Blob
    IReadOnlyList<Attachment>
```

without the caller observing that distinction.

---

### 2.4 Query shape independent from storage shape

A property does not need to exist directly in a Table entity to be queryable.

For example:

```csharp
invoice.Payload.Supplier.Address.CountryCode
```

may exist only inside a serialized blob while a derived index contains its value.

A query such as:

```csharp
repository.Query()
    .Where(x => x.Payload.Supplier.Address.CountryCode).Is("NO");
```

therefore means:

> Locate aggregates whose indexed projection of this property equals `"NO"`.

It does not mean:

> Download every payload blob and inspect it.

---

### 2.5 Derived indexes

Indexes must be considered disposable.

They may always be reconstructed from authoritative aggregate state.

If deleting an index destroys information that cannot be reconstructed, the implementation is incorrect.

---

### 2.6 Rebuildable projections

Materialized projections follow the same principle.

A projection is:

- derived,
- read-oriented,
- disposable,
- replaceable,
- versionable,
- independently rebuildable.

This follows Microsoft's Materialized View guidance, which explicitly describes materialized views as disposable read models that can be rebuilt from source data and recommends versioned or atomic publication to avoid exposing partially rebuilt views.

---

### 2.7 Explicitly finite querying

The query abstraction shall not pretend to be LINQ or SQL.

There shall be no `IQueryable<T>` implementation.

There shall be no general `IQueryProvider`.

There shall be no promise that arbitrary expression trees can execute remotely.

The supported query algebra is intentionally finite.

---

### 2.8 Efficient ID lookup

`IRepository<T>` identifies an entity using only its `Guid Id`.

Therefore the primary manifest's physical location must be derivable from `Id`.

Fetching an entity by ID must not require a table scan or secondary lookup.

---

### 2.9 Production-safe failure semantics

A failed multi-resource write must not make a partially constructed aggregate logically visible.

Orphaned physical data is acceptable.

Partially committed logical aggregates are not.

---

# 3. Non-goals

The following are explicitly outside the initial scope.

- General SQL semantics.
- `IQueryable<T>`.
- Joins between arbitrary aggregate types.
- Foreign-key enforcement.
- Multi-aggregate ACID transactions.
- Transparent distributed transactions between Blob and Table Storage.
- Arbitrary aggregation.
- Arbitrary `GroupBy`.
- Arbitrary client expressions masquerading as server queries.
- Automatic relational normalization.
- Automatic index creation from production traffic.
- Analytics workloads.
- Full-text search.
- Vector search.
- Cross-provider identical performance characteristics.
- Making every `IRepository<T>` queryable.
- Exposing physical Azure indexing structures as part of the stable public API.

If one of these becomes useful, it should be added deliberately rather than accidentally emerging from an overly general abstraction.

---

# 4. Repository structure

The implementation belongs in the existing `novolis-storage` repository.

The intended project structure becomes:

```text
src/
├── Novolis.Storage.Abstractions
├── Novolis.Storage.Query
├── Novolis.Storage.Indexing
├── Novolis.Storage.AzureTables
├── Novolis.Storage.AzureCombinedStorage
├── Novolis.Storage.InMemory
├── Novolis.Storage.Json
├── Novolis.Storage.LiteDb
├── Novolis.Storage.Ndjson
└── Novolis.Storage.Sqlite
```

The existing `Novolis.Storage.AzureTables` provider remains.

It represents direct Azure Table Storage persistence and should not be silently transformed into Azure Combined Storage.

The concepts are different:

```text
AzureTables

T
↓
Table Storage
```

versus:

```text
AzureCombinedStorage

T
├── Manifest/Table state
├── Blob state
├── Indexes
└── Projections
```

---

# 5. Package dependency model

The desired dependency direction is:

```text
                  Novolis.Storage.Abstractions
                         ▲             ▲
                         │             │
                  Storage.Query        │
                         ▲             │
                         │             │
                 Storage.Indexing      │
                         ▲             │
                         │             │
              AzureCombinedStorage ────┘
```

More specifically:

```text
Novolis.Storage.Abstractions
    no dependency on Query

Novolis.Storage.Query
    → Abstractions

Novolis.Storage.Indexing
    → Abstractions
    → Query

Novolis.Storage.AzureCombinedStorage
    → Abstractions
    → Query
    → Indexing
    → Azure.Data.Tables
    → Azure.Storage.Blobs
```

Other implementations remain free to ignore `Novolis.Storage.Query`.

For example:

```text
Novolis.Storage.Json
    → Abstractions
```

is completely valid.

So is:

```text
Novolis.Storage.Sqlite
    → Abstractions
```

until SQLite querying is deliberately implemented.

---

# 6. Stability boundaries

Not every package has the same stability expectation.

## `Novolis.Storage.Abstractions`

Stable.

Contains the fundamental repository contract and capability mechanism.

---

## `Novolis.Storage.Query`

Intended to become stable relatively early.

It defines what a query means, not how any particular provider implements it.

---

## `Novolis.Storage.Indexing`

Experimental implementation infrastructure.

Its API must not initially be considered part of the normal supported Novolis consumer surface.

Index layout, projection layout, key encoding and query-planning abstractions are expected to evolve after real usage.

If kept as a separate assembly, implementation types should preferably remain `internal` with appropriate `InternalsVisibleTo` declarations for provider assemblies.

It may still exist as a NuGet dependency for packaging purposes without presenting a useful application-facing API.

---

## `Novolis.Storage.AzureCombinedStorage`

Preview initially.

Its primary repository/query behavior may be public while advanced physical configuration remains explicitly experimental.

Any exposed configuration that has not earned stability should use .NET's experimental API mechanisms where practical.

---

# 7. Repository capabilities

Query support must be an optional repository capability.

The base abstraction does not depend on Query.

Conceptually:

```csharp
public interface IRepositoryCapabilityProvider
{
    object? GetCapability(Type capabilityType);
}
```

A generic helper may provide nicer consumption:

```csharp
public static TCapability? GetCapability<TCapability, T>(
    this IRepository<T> repository)
    where TCapability : class
    where T : IHasId
{
    if (repository is TCapability direct)
        return direct;

    return repository is IRepositoryCapabilityProvider provider
        ? provider.GetCapability(typeof(TCapability)) as TCapability
        : null;
}
```

The precise API can evolve, but the capability semantics matter more than this exact signature.

A provider may directly implement a capability.

A repository decorator should forward capabilities it does not implement itself.

This prevents decoration from accidentally destroying optional behavior.

For example:

```text
AzureCombinedRepository<T>
        │
        ▼
CachingRepository<T>
        │
        ▼
MetricsRepository<T>
```

should still expose query support even when DI resolves only:

```csharp
IRepository<T>
```

---

# 8. Query capability

`Novolis.Storage.Query` defines an infrastructure capability such as:

```csharp
public interface IRepositoryQueryProvider<T>
    where T : IHasId
{
    ValueTask<QueryPage<T>> ExecuteAsync(
        Query<T> query,
        CancellationToken cancellationToken = default);
}
```

Normal application code should not need to resolve that interface.

Instead:

```csharp
IRepository<Invoice> repository = ...;

var invoices = await repository
    .Query()
    .Where(x => x.Status).Is(InvoiceStatus.Approved)
    .Take(100)
    .ToListAsync(cancellationToken);
```

`Query()` is an extension over `IRepository<T>`.

Internally it discovers `IRepositoryQueryProvider<T>` through the repository's optional capabilities.

If unavailable, `Query()` fails immediately with an explicit query-not-supported exception.

This gives query-capable providers an ORM-like surface without changing the fundamental repository contract.

---

# 9. Query model

`Query<T>` should be an immutable description of query intent.

Conceptually:

```csharp
public sealed record Query<T>(
    IReadOnlyList<QueryPredicate> Predicates,
    IReadOnlyList<QueryOrdering> Ordering,
    int? Limit,
    string? ContinuationToken);
```

The implementation details may differ, but query construction should produce data, not executable arbitrary application code.

---

# 10. Property selectors

Expressions may be used only as strongly typed property selectors.

For example:

```csharp
.Where(x => x.Payload.Supplier.Id)
```

is valid.

The expression parser extracts a property path:

```text
Payload.Supplier.Id
```

The expression is not treated as an arbitrary remote expression.

This is invalid:

```csharp
.Where(x => Normalize(x.Payload.Supplier.Id))
```

as is:

```csharp
.Where(x => x.Amount * x.ExchangeRate)
```

unless a future explicit computed projection supports that operation.

The initial supported selector is essentially:

```text
x => x.Property
x => x.Parent.Property
x => x.Parent.Child.Property
```

This gives compile-time refactoring support while avoiding a pseudo-LINQ provider.

---

# 11. Initial query algebra

The first query algebra should remain deliberately conservative.

Expected operators:

```text
Is
IsNot
IsGreaterThan
IsGreaterThanOrEqual
IsLessThan
IsLessThanOrEqual
IsBetween
StartsWith
IsIn
```

Query structure:

```text
Where
And
OrderBy
OrderByDescending
Take
Continuation
```

Useful terminals:

```text
ToPageAsync
ToListAsync
FirstOrDefaultAsync
AnyAsync
CountAsync
```

`CountAsync` should exist only when the execution plan can perform it sensibly.

The presence of an API must imply that the provider can implement its semantics without pretending a full table scan is cheap.

---

# 12. Example query surface

The desired experience is:

```csharp
var page = await invoices
    .Query()
    .Where(x => x.OrganizationId).Is(organizationId)
    .And(x => x.Status).Is(InvoiceStatus.Approved)
    .And(x => x.Payload.InvoiceDate).IsGreaterThanOrEqual(from)
    .OrderByDescending(x => x.Payload.InvoiceDate)
    .Take(100)
    .ToPageAsync(cancellationToken);
```

This deliberately resembles an ORM while remaining a closed query language.

No LINQ translation occurs.

---

# 13. Client-side post filtering

An arbitrary local predicate may eventually be useful:

```csharp
.PostFilter(x => SomeComplicatedRule(x))
```

This must be explicitly identified as local execution.

It must never silently turn:

```csharp
repository.Query()
```

into:

```text
download the entire repository
```

A post-filter may execute only after the provider has produced a finite server-side candidate set.

A provider may reject a query where the post-filter would require an unbounded scan.

This functionality should initially be experimental.

---

# 14. Query results and continuation

Paged querying is fundamental.

A result should conceptually resemble:

```csharp
public sealed record QueryPage<T>(
    IReadOnlyList<T> Items,
    string? ContinuationToken);
```

Continuation tokens are opaque.

The application must not rely upon their internal representation.

Tokens may encode:

- physical Azure continuation information,
- index identity,
- query fingerprint,
- projection version,
- remaining order state.

A token from one logical query must not be reusable against a materially different query.

The implementation should include a query fingerprint and reject incompatible tokens.

---

# 15. Azure Combined Storage logical model

An aggregate has four possible physical representations:

```text
Aggregate
├── Manifest
├── Inline state
├── Blob-backed state
└── Derived query state
    ├── indexes
    └── materialized projections
```

Only aggregate state is authoritative.

Indexes and projections are derivative.

---

# 16. Manifest

Every aggregate has one active manifest.

The manifest represents the publication point of an aggregate revision.

It contains, conceptually:

```text
Entity ID
Entity type/storage identity
Active revision
Schema version
Deletion state
Concurrency metadata
Inline values
Blob slot references
Storage metadata
```

The exact Table Storage schema is provider-private.

The manifest is the answer to:

> Which physical pieces currently constitute this logical aggregate?

---

# 17. Primary physical addressing

Because `IRepository<T>` performs lookup by `Guid Id`, manifest location must be derivable from the GUID alone.

The default primary partition strategy therefore must not depend on a domain property such as `OrganizationId`.

Instead:

```text
PartitionKey = deterministic shard derived from hash(Id)
RowKey       = canonical Id
```

A cryptographic-strength hash is unnecessary for security, but the algorithm must be deterministic and stable.

Hashing rather than taking the leading GUID bytes is important because GUID formats such as UUIDv7 contain time-correlated high-order bits and could otherwise produce hot partitions.

A reasonable initial distribution is a fixed number of primary shards such as 256.

The exact shard algorithm is part of the physical provider format and must be versioned if it can ever change.

This gives:

```csharp
await repository.GetAsync(id);
```

a deterministic Table point lookup.

Microsoft identifies queries containing both `PartitionKey` and `RowKey` as the most efficient Table Storage query shape and warns against hot partition designs.

Tenant, organization, date and status lookup patterns belong in secondary indexes, not the primary entity locator.

---

# 18. Revisions

Every mutation creates a new physical revision.

A revision identifier must be unique.

A time-sortable identifier such as `Guid.CreateVersion7()` is suitable for physical revision IDs, although entity identity itself remains the caller-provided `Guid Id`.

Conceptually:

```text
Invoice 6f...
    revision A
    revision B
    revision C ← active manifest
```

Blob state and derived index entries include the revision they belong to.

The manifest identifies exactly one active revision.

This revision model is central to cross-store consistency.

---

# 19. Blob-backed properties

Blob-backed properties are a normal persistence primitive.

They are not merely an overflow mechanism for values exceeding Azure Table limits.

Microsoft's Large Entities pattern describes storing large property values in Blob Storage and retaining the blob address in the Table entity. It also notes that this creates multiple physical operations and requires eventual-consistency handling.

Novolis generalizes this idea.

For example:

```text
Invoice
├── Id                    manifest
├── Status                inline
├── Metadata              inline or blob
├── Payload               blob
├── ProcessingHistory     blob
└── Attachments           blob(s)
```

The application still receives one:

```csharp
Invoice
```

---

# 20. Blob granularity

The initial provider should support multiple storage granularities internally.

Likely candidates:

### Inline

Simple values supported naturally by Table Storage.

Examples:

```text
bool
integer types
floating-point types
decimal
Guid
DateTime
DateTimeOffset
enum
string
nullable forms
```

subject to Azure limits.

### Property blob

A top-level complex property is serialized independently.

For example:

```text
Payload
ProcessingHistory
Attachments
```

This permits independent evolution and potentially selective hydration.

### Aggregate payload blob

Multiple complex properties may be serialized together into a single payload when splitting them would create excessive blob operations.

The provider should not prematurely promise one universal granularity.

Request count and latency must inform the final conventions.

---

# 21. Blob references

Blob references are strongly typed inside the provider implementation.

Conceptually:

```csharp
internal readonly record struct BlobReference<T>(
    string Container,
    string Name,
    string? ETag);
```

The domain object never contains this reference.

Physical manifests may store a compact serialized representation, but internal code should avoid passing untyped blob path strings where a typed reference can preserve intent.

---

# 22. Blob layout

Blob writes are immutable per revision.

A conceptual naming scheme is:

```text
/{entity-storage-name}/{shard}/{entity-id}/{revision}/{slot}
```

For example:

```text
/invoice/7a/6f.../019.../payload.json
/invoice/7a/6f.../019.../attachments.json
```

Exact naming is private implementation detail.

Blob metadata may include:

```text
entity ID
revision
slot ID
schema version
content type
serializer/codec identifier
checksum
```

Blob containers remain private.

Blob references must never require public SAS URLs.

---

# 23. Serialization

The default structured-data serializer should be `System.Text.Json`.

Serialization must be replaceable at the slot level internally.

A codec abstraction may resemble:

```csharp
internal interface IBlobCodec<T>
{
    BinaryData Serialize(T value);
    T Deserialize(BinaryData value);
}
```

This permits future payload types such as:

- JSON
- XML
- raw binary
- compressed JSON
- protobuf
- PDFs
- images

without changing repository semantics.

Arbitrary runtime type metadata should not be persisted merely to make polymorphism convenient.

Stored type identity must remain deliberate and controlled.

---

# 24. Write pipeline

An upsert follows a prepare-and-publish model.

Given:

```csharp
await repository.UpsertAsync(invoice, cancellationToken);
```

the logical algorithm is:

```text
1. Validate aggregate
2. Resolve physical layout
3. Create new revision
4. Serialize and write immutable blobs
5. Derive new index entries from the in-memory aggregate
6. Write index entries for the new revision
7. Build any required entity-local projections
8. Validate prepared revision
9. Atomically update manifest to new revision using Table ETag
10. Schedule old derived/blob state for cleanup
```

The key rule is:

> **The manifest is published last.**

Before step 9, the new revision is physically present but logically invisible.

After step 9, the new revision becomes authoritative.

---

# 25. Why publication-last matters

Azure Table Storage and Blob Storage cannot participate in one atomic transaction.

Trying to simulate rollback across both stores introduces significantly more complexity than allowing harmless orphan data.

Instead:

```text
                 PREPARE
                    │
        ┌───────────┼───────────┐
        ▼           ▼           ▼
      blobs       indexes    projections
        │           │           │
        └───────────┴───────────┘
                    │
                 validate
                    │
                    ▼
             MANIFEST COMMIT
```

If preparation fails:

```text
old revision remains active
new physical pieces are orphaned
```

If manifest publication fails because of optimistic concurrency:

```text
winner remains active
loser's prepared pieces are orphaned
```

Orphans are maintenance work, not data corruption.

---

# 26. Concurrency

Manifest updates use Azure Table ETags.

A writer operating on an existing aggregate should publish conditionally against the observed manifest ETag.

If another writer wins first, publication fails.

The failed writer must not overwrite the successful revision.

Its already prepared blob/index state becomes eligible for garbage collection.

This allows optimistic concurrency without requiring distributed locking.

---

# 27. Index revision validation

Every index entry identifies:

```text
EntityId
EntityRevision
```

This is critical.

Consider an old index entry:

```text
Country = NO
Entity = 123
Revision = 7
```

while the manifest says:

```text
Entity = 123
Revision = 8
```

The entry is stale and must not produce a result.

Similarly, a newly prepared index entry for revision 9 may briefly exist before the manifest publishes revision 9.

The query executor therefore treats an index entry as a **candidate**, not final truth.

It verifies candidate revision against the current manifest before accepting it.

This gives the provider extremely useful failure behavior:

```text
future/unpublished index entry → rejected
old/stale index entry          → rejected
deleted entity                 → rejected
active revision                → accepted
```

Physical cleanup can therefore be lazy without compromising logical correctness.

---

# 28. Delete semantics

Deletion publishes a tombstone manifest.

Conceptually:

```text
EntityId
Revision = N
Deleted = true
```

The tombstone becomes authoritative before physical cleanup.

Any stale indexes still referring to prior revisions are ignored because manifest validation sees the tombstone.

Blob and index cleanup can happen later.

Hard deletion of the manifest itself should normally happen only through explicit retention/maintenance policy.

---

# 29. Read pipeline

A normal `GetAsync(id)` follows:

```text
Id
↓
derive manifest shard
↓
Table point lookup
↓
manifest
↓
check tombstone
↓
resolve blob slots
↓
parallel blob fetch
↓
deserialize
↓
assemble T
↓
return
```

Blob fetches should be parallelized with a bounded concurrency policy.

The provider should collect operation-count telemetry because excessive property fragmentation can otherwise quietly turn one repository read into many network operations.

---

# 30. Indexes

An index is a provider-managed mapping from some derived property value or values to aggregate candidates.

Conceptually:

```text
Index value
    ↓
EntityId + Revision
```

An index may represent:

```text
SupplierId
Status
InvoiceDate
CountryCode
OrganizationId + Status
OrganizationId + Status + InvoiceDate
```

Index values may come from inline or blob-backed properties.

The physical location of the original property is irrelevant.

---

# 31. Write-time index extraction

Indexes should normally be produced while the authoritative aggregate already exists in memory during an upsert.

Given:

```csharp
invoice.Payload.Supplier.Address.CountryCode
```

the write pipeline already has access to `"NO"`.

Therefore:

```text
Invoice
   ├── serialize Payload ───────────► Blob
   ├── extract CountryCode ─────────► Index
   ├── extract SupplierId ──────────► Index
   └── publish revision ────────────► Manifest
```

is preferred to:

```text
write blob
↓
later download blob
↓
deserialize blob
↓
derive index
```

The latter is reserved primarily for rebuilding indexes from authoritative stored state.

---

# 32. Index definitions

Index definitions are initially an internal or experimental concept.

A conceptual definition might look like:

```csharp
Index(x => x.Status);

Index(x => x.Payload.Supplier.Id);

Index(
    x => x.OrganizationId,
    x => x.Status,
    x => x.Payload.InvoiceDate);
```

This syntax is illustrative.

The stable API must not promise this exact shape until real-world usage answers questions around:

- nulls,
- collections,
- nested optional objects,
- uniqueness,
- string normalization,
- case sensitivity,
- multiple values,
- ranges,
- compound key order,
- index migration,
- index versioning,
- hot partitions,
- calculated values.

---

# 33. Composite indexes

Composite indexes should be supported internally from the beginning.

For example:

```text
OrganizationId
Status
InvoiceDate
```

may physically become an index where:

```text
PartitionKey = encoded OrganizationId + Status
RowKey       = encoded InvoiceDate + EntityId + Revision
```

This can support:

```text
Organization = X
Status = Approved
InvoiceDate >= date
OrderBy InvoiceDate
```

through efficient Table range operations.

The physical layout is provider-private.

Microsoft explicitly recommends compound keys and duplicated/denormalized Table representations where these align storage with dominant query patterns.

---

# 34. Index key encoding

Index encoding must be deterministic and sortable where range semantics are required.

A dedicated encoding subsystem should exist.

It must define behavior for:

```text
null
bool
signed/unsigned numbers
decimal
Guid
DateTime
DateTimeOffset
enum
string
```

Ordering must not accidentally depend on:

- current culture,
- machine locale,
- `ToString()`,
- runtime implementation details.

For range-supporting numeric values, encoding must preserve logical order under lexicographic Table key ordering.

Strings should initially use explicitly documented ordinal semantics.

Case-insensitive or culture-aware indexes should not be guessed into existence.

They can later be explicit index variants.

---

# 35. Index identity

Physical index identity must not rely solely on CLR property names.

Renaming:

```csharp
SupplierId
```

to:

```csharp
VendorId
```

must not automatically destroy production storage semantics.

Index definitions therefore need stable internal IDs or versioned physical names.

The same applies to entity and blob slot identities.

Default conventions may derive names from CLR types during early development, but production adoption requires migration-safe logical identities.

---

# 36. Query planning

The query provider converts:

```text
Query<T>
```

into an internal execution plan.

The planner considers:

1. primary key lookup,
2. direct manifest Table filtering where sensible,
3. single-field indexes,
4. composite indexes,
5. covering indexes,
6. materialized projections,
7. whether client post-filtering is bounded,
8. whether requested ordering is natively satisfied.

The planner must prefer bounded access paths.

An unbounded Table scan should not occur silently.

---

# 37. Query failure

A query with no acceptable execution strategy should fail explicitly.

For example:

```csharp
repository
    .Query()
    .Where(x => x.Payload.SomeUnindexedProperty).Is(value)
```

may result in:

```text
QueryNotSupportedException

No query path can satisfy:
Payload.SomeUnindexedProperty == ...
```

That is preferable to quietly scanning ten million aggregates.

An explicit future opt-in to scans could exist, but it should never be the default.

---

# 38. Query planning diagnostics

The internal planner should produce a diagnostic representation even if no stable public `Explain()` API initially exists.

For example:

```text
Entity: Invoice
Plan: SecondaryIndex
Index: organization-status-date
Partition: <encoded>
Range: <encoded>
Ordering: Native
Candidate hydration: Required
Post-filter: None
```

This should be logged at appropriate diagnostic levels and available to tests.

A future public:

```csharp
repository.Query().Explain()
```

can be added after the plan model stabilizes.

---

# 39. Projections

The provider shall distinguish authoritative aggregate state from materialized read projections.

A projection may contain:

```text
EntityId
Revision
SupplierName
Status
InvoiceDate
Currency
Total
```

even though those values originate from several aggregate properties.

This allows a query-oriented structure to be shaped specifically for reads.

Microsoft's Materialized View pattern explicitly supports derived fields, transformations and read models optimized for a particular query or small group of queries.

---

# 40. Projection classes

Two projection classes should be distinguished internally.

### Entity-local projection

Derived from one aggregate.

Essentially a richer or covering index.

It may contain enough information to filter/order candidates without loading the full aggregate.

### Cross-entity materialized view

Derived from multiple aggregates or entities.

For example:

```text
Organization
Month
InvoiceCount
TotalAmount
```

This is materially more complex.

Cross-entity projections should not be required for the initial Azure Combined provider but the architecture must not prevent them.

---

# 41. Projection publication

Large or cross-entity projections should be versioned.

Rebuilding follows:

```text
existing projection V4 remains active
        ↓
build V5
        ↓
validate V5
        ↓
publish pointer → V5
        ↓
retire V4 later
```

Readers must never observe half of a rebuilt projection.

This follows Microsoft's recommendation to use atomic publication or versioned replacement when materialized-view regeneration can fail part way through.

---

# 42. Projection APIs are not initially public

The initial public query abstraction should return aggregates.

For example:

```csharp
IReadOnlyList<Invoice>
```

A future API may support typed read projections:

```csharp
.Query()
.Project(...)
```

but this should not be designed prematurely.

Projection storage can exist internally before a public projection language does.

---

# 43. Index rebuilding

Rebuilding indexes is a normal operation.

Conceptually:

```text
enumerate authoritative manifests
↓
hydrate required aggregate state
↓
derive index entries
↓
write new index version
↓
validate
↓
publish index version
↓
retire previous index
```

A rebuild must not require application downtime.

Where feasible, the previous complete index remains queryable until the replacement is ready.

---

# 44. Repair

The provider should eventually expose maintenance operations separate from normal application repository behavior.

Required repair capabilities include:

```text
detect orphan blobs
detect orphan index revisions
detect manifests with missing blobs
verify index completeness
rebuild indexes
rebuild projections
remove expired revisions
validate checksums
```

Maintenance APIs are operational infrastructure and need not be part of `IRepository<T>`.

---

# 45. Garbage collection

The revision model naturally creates garbage.

Examples include:

- failed prepared revisions,
- blobs from concurrency losers,
- old successful revisions,
- stale index rows,
- replaced projection versions.

Garbage collection uses manifest/reference reachability.

An artifact is not eligible immediately after becoming unreferenced.

A grace period protects against:

- long-running writes,
- delayed cleanup,
- temporary retry scenarios,
- clock skew.

Cleanup must be idempotent.

---

# 46. Consistency model

The provider should document consistency explicitly.

## Direct ID reads

Direct reads resolve the currently published manifest revision.

They should therefore observe only a complete published aggregate.

## Queries

Queries are based on derived structures.

They may therefore have different freshness characteristics depending on index/projection implementation.

For synchronous revision indexes written before manifest publication, queries can achieve very strong logical behavior because unpublished and stale entries are rejected through manifest revision validation.

Asynchronous materialized views remain eventually consistent.

Microsoft explicitly notes that secondary structures and materialized views introduce consistency tradeoffs and may require eventual consistency.

The API must not claim stronger consistency than the selected query path can provide.

---

# 47. Query candidate verification

For normal aggregate queries, correctness wins over stale-index convenience.

The expected path is:

```text
index lookup
↓
candidate EntityId + Revision
↓
manifest point lookup
↓
revision/deletion validation
↓
hydrate accepted aggregate
```

Manifest lookups may be parallelized and batched where the Azure API permits.

This verification is what allows lazy index cleanup without returning incorrect entities.

---

# 48. Covering projections

A future optimization may permit a projection to satisfy a query without full aggregate hydration.

This is safe only when the projection itself has an explicit publication/version contract.

The normal assumption remains:

```text
index = candidate locator
manifest = authority
```

until a projection can independently establish its validity.

---

# 49. Blob request economics

Blob-heavy storage trades schema flexibility for network operations.

The provider must therefore instrument:

```text
blobs read per aggregate
blobs written per revision
bytes read
bytes written
parallel fetch duration
manifest requests
index requests
```

If an ordinary aggregate read consistently requires:

```text
1 Table read
12 Blob reads
```

the physical layout likely needs consolidation.

The architecture deliberately permits this to be changed without changing `T`.

---

# 50. Automatic physical placement

Initial placement should rely on conservative conventions plus provider configuration.

Possible default:

```text
simple scalar property → inline candidate
complex top-level property → blob candidate
large binary property → blob
collection → blob candidate
```

These are implementation conventions, not semantic rules.

The provider may later learn that some complex properties are better serialized together.

Real-world telemetry should drive that decision.

---

# 51. Physical placement overrides

Advanced users will eventually need explicit control.

A future experimental configuration might resemble:

```csharp
storage.Entity<Invoice>()
    .Blob(x => x.Payload)
    .Blob(x => x.Attachments)
    .Inline(x => x.Metadata);
```

This should not be treated as stable in the first release.

The underlying capability should exist before the fluent API becomes contractual.

---

# 52. Index configuration

Likewise:

```csharp
storage.Entity<Invoice>()
    .Index(x => x.OrganizationId)
    .Index(x => x.Status)
    .Index(x => x.Payload.Supplier.Id)
    .Index(
        x => x.OrganizationId,
        x => x.Status,
        x => x.Payload.InvoiceDate);
```

is a useful conceptual configuration surface.

It should initially be considered experimental.

The most likely source of bad early API decisions is not repository behavior. It is index-definition semantics.

That API should earn stability through usage.

---

# 53. Schema evolution

Every manifest and blob should carry sufficient storage schema identity to support migrations.

At minimum:

```text
storage schema version
serializer/codec version
logical entity storage identity
```

Reading an old revision may perform an in-memory upgrade before materializing `T`.

Physical rewrites should be separate explicit operations where possible.

Adding a new non-indexed field inside a JSON blob should usually require no Table schema migration.

This is one of the design's major advantages.

---

# 54. Adding an index

Consider:

```csharp
public PaymentTerms Terms { get; init; }
```

being added inside an existing blob payload.

Initially no index is required.

Later:

```text
Terms.DueDate
```

becomes important for querying.

The system should be able to add a new index definition and backfill it from authoritative aggregates without rewriting the aggregate schema.

This is an intended first-class workflow.

---

# 55. Removing an index

Because indexes are derived, removing one means:

```text
stop planning queries through it
↓
retire index
↓
delete physical index state
```

Aggregate data is unaffected.

---

# 56. Changing an index

An incompatible index change creates a new index version.

Never mutate the meaning of existing encoded rows in place.

Use:

```text
Index V1 active
↓
build V2
↓
validate
↓
switch planner/catalog to V2
↓
retire V1
```

---

# 57. Query provider implementations

`Novolis.Storage.Query` should be reusable by providers other than Azure Combined Storage.

Potential implementations include:

```text
AzureCombinedStorage
    Query<T> → index/materialized-view plan

AzureTables
    Query<T> → Table/OData filters where supportable

Sqlite
    Query<T> → SQL

LiteDb
    Query<T> → LiteDB query model

InMemory
    Query<T> → direct evaluation
```

There is no requirement that all providers support Query.

There is also no requirement that every provider support every query operator.

Capability and query validation are explicit.

---

# 58. Provider-specific capability negotiation

A query provider should expose supported semantics internally.

For example:

```text
Equal            yes
Range            yes
StartsWith       yes
In               bounded
Ordering         index-dependent
Count            plan-dependent
PostFilter       bounded candidates only
```

The query abstraction defines meaning.

Providers define whether they can fulfill that meaning efficiently and correctly.

---

# 59. Existing Azure Tables provider

`Novolis.Storage.AzureTables` remains simpler.

It may eventually implement `Novolis.Storage.Query`, translating supported queries directly to Table Storage.

That is independent of Azure Combined Storage.

No dependency on `Novolis.Storage.Query` should be added merely for architectural symmetry.

It should be added only when that provider actually supports the capability.

---

# 60. In-memory query implementation

`Novolis.Storage.InMemory` is an especially useful future Query implementation.

It can execute `Query<T>` directly against stored entities.

This provides:

- deterministic tests,
- semantic verification of query operators,
- a reference implementation,
- comparison against Azure execution.

The in-memory implementation should reproduce query semantics, not Azure physical behavior.

Azure physical behavior is tested separately.

---

# 61. Testing strategy

Testing should be layered.

## Query semantic tests

Pure tests verify:

```text
property selector parsing
operator semantics
ordering
continuation validation
query fingerprints
unsupported expressions
post-filter rules
```

## Index encoding tests

Property-based or large generated test sets should verify:

```text
Encode(a) < Encode(b)
```

whenever:

```text
a < b
```

for range-capable encodings.

Boundary conditions matter heavily.

## Planner tests

Given:

```text
query
available indexes
available projections
```

assert the selected plan.

## Provider integration tests

Use real Azure-compatible storage infrastructure, preferably Azurite for normal local/CI integration tests.

Avoid mocking the Azure SDK when an actual storage emulator can exercise the behavior.

## Azure parity tests

Any behavior dependent on Azure-specific semantics that cannot be trusted solely through an emulator should have an optional real-Azure integration suite.

---

# 62. Critical concurrency tests

At minimum:

```text
two concurrent updates
writer failure after blobs
writer failure after some indexes
writer failure before manifest
manifest ETag conflict
cleanup during write
read during preparation
query during preparation
query after manifest publish but before cleanup
delete during query
index rebuild while writes continue
projection version switch during reads
```

The most important invariant:

> A caller must never reconstruct an aggregate from pieces belonging to different revisions.

---

# 63. Fault injection

Azure Combined Storage deserves deliberate fault injection.

Tests should be able to fail:

```text
Nth blob write
Nth index write
manifest publication
blob read
index read
cleanup
projection publication
```

Then verify:

```text
previous revision remains valid
new partial revision remains invisible
repair can recover
garbage collection eventually removes leftovers
```

This is more valuable than mocking every Azure call.

---

# 64. Observability

The provider should emit structured diagnostics through normal `Microsoft.Extensions.*` abstractions.

Useful dimensions include:

```text
entity type
operation
revision
query fingerprint
plan type
index identity
projection identity
candidate count
accepted count
stale candidates
manifest reads
blob reads
blob writes
bytes transferred
query duration
hydration duration
projection age
```

Avoid logging complete serialized aggregate values by default.

---

# 65. Useful metrics

Candidate metrics:

```text
novolis.storage.read.duration
novolis.storage.write.duration
novolis.storage.query.duration
novolis.storage.blob.read.count
novolis.storage.blob.write.count
novolis.storage.query.candidates
novolis.storage.query.stale_candidates
novolis.storage.index.rebuild.duration
novolis.storage.projection.age
novolis.storage.orphan.count
novolis.storage.concurrency.conflicts
```

Exact telemetry naming can follow broader Novolis conventions.

---

# 66. Security

The provider shall assume private storage.

Production authentication should support `TokenCredential` and managed identity naturally.

Connection strings remain useful for development and test scenarios.

Blob containers should not require public access.

Domain entities must never receive raw credentials or signed blob URLs as part of hydration.

Index and projection stores must be treated as containing potentially sensitive duplicated data.

An index over:

```text
Email
NationalIdentityNumber
SupplierBankAccount
```

creates another copy of that information.

The storage abstraction cannot make this harmless.

Applications must choose indexed fields with the same data-classification care as ordinary persistence.

---

# 67. Tenant isolation

Partitioning and indexes are performance/data-layout mechanisms.

They are not security boundaries.

If an application requires tenant/organization isolation, authorization must occur independently of storage addressing.

The query API must not create an assumption that:

```text
OrganizationId in index
```

is equivalent to authorization.

---

# 68. Cost model

The design accepts write amplification deliberately.

One aggregate write may involve:

```text
1..N blob writes
1..N index writes
0..N projection writes
1 manifest update
```

This is justified only where read/query behavior benefits from the derived structures.

Microsoft's Index Table guidance explicitly identifies the tradeoff between faster reads and the overhead of maintaining duplicate index structures.

Indexes should therefore correspond to real access patterns, not speculative enthusiasm.

---

# 69. Read/write philosophy

The architecture intentionally favors:

```text
write once into authoritative shape
derive explicit read structures
query those structures efficiently
```

over:

```text
store everything generically
scan it later
```

The design is therefore closer to a small document-store/query-projection engine than a traditional ORM.

---

# 70. Conceptual comparison

A relational ORM typically does:

```text
object graph
↓
normalize/flatten into relational model
↓
query relational model
```

Azure Combined Storage does:

```text
object graph
↓
store aggregate-shaped state
+
derive query-shaped indexes/projections
```

These are deliberately separate concerns.

The domain model says:

> This data belongs together.

Physical blob placement says:

> This is how the state should be persisted efficiently.

Index definitions say:

> This is how we need to find it.

Projection definitions say:

> This is how we need to read it.

Those concerns should not be collapsed into one model.

---

# 71. Example

Given:

```csharp
public sealed record Invoice(
    Guid Id,
    Guid OrganizationId,
    InvoiceStatus Status,
    InvoicePayload Payload,
    IReadOnlyList<Attachment> Attachments) : IHasId;
```

and:

```csharp
public sealed record InvoicePayload(
    Supplier Supplier,
    DateOnly InvoiceDate,
    string Currency,
    decimal Total);
```

the physical layout might eventually be:

```text
Manifest
    Id
    Revision
    Status
    PayloadBlob
    AttachmentsBlob

Payload blob
    Supplier
    InvoiceDate
    Currency
    Total

Attachments blob
    [...]

Index: organization-status-date
    OrganizationId
    Status
    InvoiceDate
    EntityId
    Revision

Index: supplier
    Supplier.Id
    EntityId
    Revision
```

Application code remains:

```csharp
var invoice = await repository.GetAsync(invoiceId, cancellationToken);
```

and:

```csharp
var invoices = await repository
    .Query()
    .Where(x => x.OrganizationId).Is(organizationId)
    .And(x => x.Status).Is(InvoiceStatus.Approved)
    .And(x => x.Payload.InvoiceDate).IsGreaterThanOrEqual(from)
    .OrderByDescending(x => x.Payload.InvoiceDate)
    .Take(100)
    .ToListAsync(cancellationToken);
```

No storage DTO appears in either use case.

---

# 72. Expected query execution

The example query may become:

```text
Query<Invoice>
        ↓
match organization-status-date index
        ↓
encode OrganizationId + Approved
        ↓
Table range query using InvoiceDate
        ↓
candidate (EntityId, Revision)[]
        ↓
parallel manifest point reads
        ↓
discard stale/deleted revisions
        ↓
hydrate active blob state
        ↓
Invoice[]
```

The query API knows none of these physical details.

---

# 73. Failure example

Suppose revision 17 is current.

A writer attempts revision 18:

```text
write payload-18       ✓
write attachments-18   ✓
write index A-18       ✓
write index B-18       ✗
```

The operation fails.

Manifest still points to revision 17.

Therefore:

```text
Get → revision 17
Query → revision 17
```

Revision 18 artifacts are garbage.

No logical rollback is required.

Later maintenance removes them.

---

# 74. Concurrency example

Two writers begin from revision 17.

```text
Writer A prepares 18A
Writer B prepares 18B
```

Both successfully prepare blobs and indexes.

Writer A conditionally updates the manifest first.

```text
Manifest → 18A
```

Writer B's conditional update fails due to ETag mismatch.

Now:

```text
18A = authoritative
18B = orphaned
```

Any index entry for 18B is ignored because manifest validation reports 18A.

This is an intentional consistency mechanism, not merely cleanup behavior.

---

# 75. Index rebuild example

Suppose a new query requires:

```csharp
x => x.Payload.Supplier.Address.CountryCode
```

A new index is introduced.

The provider:

```text
creates index version 1
↓
enumerates active manifests
↓
hydrates Payload
↓
extracts CountryCode
↓
writes index entries
↓
validates completeness
↓
marks index available to planner
```

During rebuild, existing repository operations continue.

Queries requiring the new index remain unsupported until the index is ready rather than silently scanning the store.

---

# 76. Materialized-view example

A future requirement may ask:

```text
For each organization and month:
    approved invoice count
    approved total
    currency
```

That should not require every request to hydrate every invoice.

A materialized projection may maintain:

```text
OrganizationId
YearMonth
Currency
ApprovedCount
ApprovedTotal
ProjectionVersion
```

The projection remains rebuildable from authoritative invoices.

Applications do not update it directly.

This is precisely the class of problem Microsoft's Materialized View pattern addresses.

---

# 77. Public API philosophy

The public surface should remain much smaller than the implementation.

A consumer should conceptually understand only:

```text
IRepository<T>
repository.Query()
Query<T>
QueryPage<T>
```

plus normal provider registration.

They should not need to understand:

```text
IndexEntry
IndexVersion
ProjectionManifest
BlobSlot
StorageRevision
IndexKeyEncoder
QueryPlan
ManifestEntity
```

Those belong inside the machine room.

---

# 78. Why indexing remains experimental

Indexing looks trivial until it encounters production semantics.

Questions that need evidence before stabilization include:

```text
Should null be indexed?
Can one entity emit multiple entries for a collection?
How are dictionaries indexed?
Do strings have ordinal or normalized semantics?
How does StartsWith interact with normalization?
How are decimals encoded?
What constitutes a unique index?
Can indexes be sparse?
What happens when a nested parent is null?
Can indexes contain calculated values?
How should compound indexes expose prefix behavior?
What is the correct migration model?
When does a projection become preferable to an index?
```

The architecture should support answering these questions experimentally without forcing compatibility with the first answer.

---

# 79. Implementation phases

## Phase 1: Query model

Create `Novolis.Storage.Query`.

Implement:

```text
Query<T>
property paths
finite predicates
ordering
Take
continuation model
QueryPage<T>
query capability discovery
extension over IRepository<T>
```

Create semantic tests independent of Azure.

---

## Phase 2: Azure Combined core

Create `Novolis.Storage.AzureCombinedStorage`.

Implement:

```text
manifest
deterministic ID sharding
revision model
inline state
blob-backed properties
serialization
Get
Upsert
Delete/tombstone
ETag concurrency
```

No sophisticated indexes required yet.

---

## Phase 3: Internal indexing

Create `Novolis.Storage.Indexing`.

Implement:

```text
index descriptors
canonical value encoding
single-field indexes
compound indexes
revision-scoped entries
query planner
candidate validation
```

Indexes remain implementation-facing.

---

## Phase 4: Query integration

Connect:

```text
Query<T>
↓
planner
↓
Azure index execution
↓
manifest validation
↓
hydration
```

Reject unsupported/unbounded plans.

---

## Phase 5: Rebuild and repair

Implement:

```text
index rebuild
orphan detection
stale entry cleanup
blob cleanup
integrity verification
maintenance telemetry
```

This phase is required before calling the provider production-hardened.

---

## Phase 6: Materialized projections

Introduce internal projection infrastructure where demonstrated by real access patterns.

Use versioned publication.

Do not add a public projection DSL yet.

---

## Phase 7: API hardening

Review production usage.

Only then decide whether concepts such as:

```csharp
.Index(...)
.Blob(...)
.Inline(...)
.Project(...)
```

deserve stable public APIs.

---

# 80. Acceptance criteria for an initial usable release

The provider can be considered functionally usable when all of the following hold:

1. Any `T : IHasId` supported by serialization can be persisted and retrieved.
2. Complex aggregate properties can be stored in Blob Storage transparently.
3. `GetAsync(Guid)` performs deterministic point lookup without scanning.
4. Concurrent writes cannot publish mixed revisions.
5. Failure before manifest publication leaves the previous revision authoritative.
6. Deletes immediately become logically invisible through tombstones.
7. A query can resolve through at least one secondary index.
8. Stale and unpublished index revisions never become accepted query results.
9. Unsupported queries fail explicitly rather than silently scanning.
10. Index state can be reconstructed from authoritative aggregate state.
11. Orphaned physical data can be identified and safely cleaned.
12. Integration tests demonstrate the behavior against actual Azure-compatible Table and Blob services.
13. Storage operation counts and query plans are observable.
14. No Azure persistence types leak into domain objects.
15. Providers that do not support Query remain independent of `Novolis.Storage.Query`.

---

# 81. Architectural invariants

These should be treated almost as laws of the implementation.

### Invariant 1

**`IRepository<T>` does not imply queryability.**

### Invariant 2

**The manifest determines the authoritative revision.**

### Invariant 3

**A revision is immutable after preparation.**

### Invariant 4

**Blob state may contain truth. Index state may not.**

### Invariant 5

**Every index and materialized projection is reconstructable.**

### Invariant 6

**Index candidates must be validated against authoritative revision state unless the projection has an equivalent versioned publication guarantee.**

### Invariant 7

**Unsupported queries fail rather than unexpectedly scanning the repository.**

### Invariant 8

**Domain types do not know their Azure physical representation.**

### Invariant 9

**Query intent does not expose physical index names or layouts.**

### Invariant 10

**Physical layout may evolve without requiring a domain-model redesign.**

---

# 82. Design mantra

The system can be summarized as:

> **Aggregates hold truth. Blobs hold rich state. Tables hold identity and coordination. Indexes hold access paths. Projections hold read shapes. Query expresses intent.**

Or, in storage-flow form:

```text
                         T
                         │
              authoritative aggregate
                         │
             ┌───────────┴───────────┐
             ▼                       ▼
          Manifest                Blob state
             │                       │
             └───────────┬───────────┘
                         │
                  source of truth
                         │
                 ┌───────┴────────┐
                 ▼                ▼
              indexes         projections
                 │                │
                 └───────┬────────┘
                         ▼
                     Query<T>
```

That is the intended Novolis Storage model.

It borrows heavily from Microsoft's own Azure Storage guidance rather than attempting to force relational behavior onto Table and Blob Storage: model for efficient access patterns, denormalize when useful, use explicit secondary indexes where the store lacks them, store large state in Blob Storage, and treat read-optimized projections as derived state.

The Novolis-specific contribution is the strongly typed composition:

```text
IRepository<T>
+
optional Query<T>
+
transparent Table/Blob composition
+
revision-safe derived indexes
+
rebuildable projections
```

without pretending the result is a relational database.