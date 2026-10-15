### Step 1.1: Single Responsibility → Unix Philosophy
**"One class, one reason to change"**

**Problem**: `ProductService` handles catalog, pricing, stock, supplier notification, and transport — all at once.

**Plan**:
- Identify every distinct concern currently living in `ProductService` and `Product`
- Extract each concern into its own class with a single focus
- Use the domain vocabulary to name each class (not `ProductCatalogManager` — just `Catalog`)



## ** Exercise **

extract a Product smallest entity that fits the Storage BC

in the Storage sub domain, keep only properties that are usefull for stocking the merchandise in a Warehouse

same for  catalog, pricing, supplier notification, and transport

## Assesment

run your code through our harness to verify you succeed this part:

```
./cupid-step-1-1.harness.sh
```

Deterministic and Roslyn-based (see `Cupid.Harness`), not AI-guided - same code in, same
pass/fail out, every time. Rule implementations and their pinning tests live in
`Cupid.Harness/Rules/` and `Cupid.Harness.Test/`.


### HA1 - C# idioms

use FxCop to enforce C#14 style


### HA5 - no god class

no class shall have more than 6 public properties

### HA6 - Single Concern

a class's public methods must belong to at most one bounded-context concern (Catalog, Pricing,
Storage, Supplier, Sales, Lifecycle, Notification, Transport - see
`Cupid.Harness/ubiquitous-language-map.json`). A method is assigned to a concern by matching its
name against that concern's vocabulary. A class whose public API spans two or more concerns is
mixing responsibilities - the exact `ProductService`/`Product` smell this exercise targets.

### HA7 - Fan-out

applies only to entities - types declared under `OmniProduct_CoreDomain.Models.*` that implement
`IDentifiable` (see HA9). An entity may directly reference (via fields, constructor/method
parameters, or local variables) at most 3 distinct entity types, using that same definition - not
every domain type and not BCL/framework types. Value objects and services don't count toward the
cap, and neither does an `IDentifiable` implementer declared outside `Models.*` (HA9 should
already forbid that; HA7 checks the namespace itself rather than relying on it). Non-entity
classes (services, value objects) are entirely out of scope for this rule - a service wiring
together many entities is expected and is HA10's concern, not HA7's. An entity wiring together
many unrelated entities is orchestrating too many concerns even if it stays under the HA5 property
cap or doesn't trip HA6's vocabulary check.

### HA8 - No Persistence in Domain

a domain model must not know how it is stored. Flags, on any type that is not itself EF
infrastructure (a `DbContext` subclass):
- EF Core data-annotation attributes (`[Table]`, `[Column]`, `[Key]`, `[NotMapped]`,
  `[ForeignKey]`, ...) used directly on the domain type
- hand-rolled "shadow properties" that flatten a real domain value for the ORM (any property
  ending in `Csv` or `Json`)
- sync/hydrate methods that exist only to keep those shadows in agreement with the real domain
  fields (`SyncEfColumns`, `HydrateFromEfColumns`, or any `Sync*`/`Hydrate*`/`*ToEntity`/
  `*FromEntity` method)

This is the persistence-concern heuristic: instead of folding "persistence" into HA6's vocabulary
map (which would only catch it as "one more concern among others"), HA8 makes it non-negotiable -
a domain type either stays free of ORM vocabulary entirely, or the harness fails, regardless of
how many other concerns it mixes in. Move the mapping into the `DbContext` (fluent API) or a
dedicated infrastructure/mapping type instead.

### HA9 - Entities must implement `IDentifiable`

closes a loophole HA7 would otherwise leave open: HA7 only counts fan-out toward types that
implement `IDentifiable`, so a type could dodge the fan-out cap simply by not implementing the
interface. HA9 makes the namespace/interface pairing mandatory - every class or record declared
under `OmniProduct_CoreDomain.Models` (or a nested namespace under it) must implement
`IDentifiable`. If a type in that namespace is really a value object or service, move it out of
`Models`; if it's an entity, implement `IDentifiable`.

### HA10 - No services in entity constructors

an entity (a type implementing `IDentifiable`, see HA9) must not take a service - a type living
under `OmniProduct_CoreDomain.Services.*` or simply named `*Service` - as a constructor parameter
(including a record's primary constructor). An entity models data and identity; it should be
constructible from plain values, value objects, or other entities. Accepting a service inverts the
dependency direction the workshop is teaching - services depend on entities, never the other way
round. Move the orchestration into the service layer instead.

### HA12 - Identifiers are rich objects (UUID v7+ or ULID)

every `Id` / `*Id` / `*Ids` property, field, local, return type or parameter of the model and events (anything outside
a `Services` namespace) must be a `Guid` or a `Ulid`, or a record wrapping exactly one of them
(`record ProductId(Guid Value)`). A `string`, `int` or `long` identifier is a violation. For `*Ids` collections and
dictionaries the element and key types are checked. "At least v7" is checked by shape: `Guid.NewGuid()` (v4),
`Guid.Empty`, `new Guid()` and `default(Guid)` are flagged where they build an identifier; `Guid.CreateVersion7()` and
`Ulid.NewUlid()` are fine.

**HA12.1 (warning only, `[WARN]`):** primitive obsession on identifiers, services included. Any id member whose type is
not declared in the analysed source (so a BCL or package type used raw: `string`, `int`, `Guid`, `Ulid`, `Uri`, ...)
gets a warning asking for a domain record, so a `ProductId` cannot be mixed up with a `SupplierId`. A `string` id
therefore both fails HA12 and warns in HA12.1.
