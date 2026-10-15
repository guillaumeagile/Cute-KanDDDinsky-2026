

refactor Price , to extract (and remove) the VAT entity


composable:  so that Retail Accounting is able to calculate retail price based on Margin

note that Net Price will calculate by applying several rules,  first apply the Margin, then may be TransportationFee, then VAT if applyable



## Assesment

pass the harness verification

-> invoquer   cupid-step-1-2.harness.sh =>  code review + mutation testing + analyser roslyn + linter

## Harness (HA)

### HA1 - C# idioms

use FxCop to enforce C#14 style 

### HA2 - immutable data structures

enforce the usage of records and all imutable collections
no setter on any property

### HA3 - Records are Value Objects, never entities

a record must never implement `IDentifiable` - identity belongs to entities (HA9), and a record's
job is to be a Value Object: immutable, equality by value, no identity. HA2 already forbids
setters and HA5 (step 1.1) already caps public properties at 4; this rule adds record != entity and, on every value object, no mutable field and no mutable collection. The `OmniProduct_CoreDomain.ValueObjects.*` namespace also holds records only: a `class` or plain `struct` declared there is a violation.

This rule also pins the concrete deliverable of this step: a record named `Price` must exist
under `OmniProduct_CoreDomain.ValueObjects`. `Price` is the running Value Object example - refactor it
to drop the VAT entity, and keep it composable (Margin, then TransportationFee, then VAT) while
staying an immutable record.

### ADT (algebraic data structures) — covered by HA4.3

Represent closed alternatives with `OneOf<T0, T1, ...>` from the `OneOf` NuGet package. Its `.Match(...)` API forces each alternative to be handled, without relying on an inheritance hierarchy. This also satisfies HA4's no-inheritance constraint.

Use these unions for expected outcomes, such as `OneOf<Product, ProductNotFound>` for a lookup or a union of success and named domain failures for an operation. Do not encode expected absence as `null`. Chain operations with narrowly scoped `Bind`/`Map` helpers that propagate each failure case; `OneOf` supplies exhaustive matching but does not itself provide monadic chaining. Keep unexpected failures exceptional rather than silently converting them into domain results.

The harness checks this through HA4.3: `Product` must have at least one `OneOf<...>` property. Record hierarchies alone do not satisfy HA4.3.

You will gain OneOf . but also Result and Option, that are sum types, which are ADTs, and OneOf already expresses both:
- Option<T> is OneOf<T, None> or OneOf<T, NotFound>.
- Result<T, E> is OneOf<T, E> or OneOf<T, Error>.

OneOf.Types ships None, NotFound, Success, Error<T> and others.

#### Exercice 📝


use Result to avoid exceptions
- That means StoredProduct.Withdraw and the lookups that currently throw new Exception.
- Use a specific error union such as OneOf<Withdrawn, InsufficientStock> rather than a generic Result<T, string> or Result<T, Exception>.
- The failure cases are then a closed set that the compiler checks exhaustively.
- Keep exceptions for bugs and violated invariants, not for business outcomes.

Optional: low priority
- C# 14 nullable reference types (Supplier?) already cover most of it.
- Use OneOf<Supplier, NotFound> only for lookups where absence is a normal answer and you want the caller forced to handle it. FindSupplierForRegion and GetStock are examples.
- The "TransportationFee if any, VAT if applicable" case needs no Option. An absent adjustment is simply missing from the ordered PriceAdjustment list, which is the composable design you want.

### HA4 - CUPID Principle: Composable = Extend through composition, not modification

no inheritance, verified by static code analysis; model alternatives with OneOf rather than record inheritance.

#### Why

When two kinds of product behave differently, the reflex is `PerishableProduct : Product` and
`NonExpiringProduct : Product`.

That extends `Product` by inheritance: every new kind adds a subclass,
and every consumer that needs to know the kind ends up with `is`/`as` checks. HA4 asks for the opposite:
keep `Product` as it is and **compose** it with a value that carries the difference.

The rule is checked by static analysis: no class or record may declare a base type other than
`object` (interfaces are fine). This includes the `OneOfBase<...>` helper class of the OneOf package,
which is inheritance in disguise - use `OneOf<T0, T1>` itself instead.

#### Exercise 📝

Products now have a shelf life, and the two kinds differ in their dates, not in what a product is:

| Kind | Dates | Meaning |
|---|---|---|
| **Perishable** | `SellByDate`, `UseByDate` | After `SellByDate` the product can no longer be sold. After `UseByDate` it is unsafe to consume. Invariant: `SellByDate <= UseByDate`. |
| **NonExpiring** | `BestBeforeDate` | Advisory only: a quality guideline. The product can still be sold after it, but the catalog label should say so. |

1. **Create the two value objects** as immutable records in `ValueObjects` (HA2).  This is what makes illegal combinations unrepresentable.
2. **Give `Product` one `ShelfLife` property** of type `OneOf<Perishable, NonExpiring>`, set once at
   creation (no setter).  
3. **Validate the invariant at the boundary.** Dates are input data, so a `Perishable.Create(...)` that
   returns `OneOf<Perishable, InvalidDates>` is better than a constructor that throws (HA4.2).
4. **Put the behaviour behind `Match`**, so each alternative is handled and the compiler checks it:
   `Product.CanSell(today)` returns `OneOf<Sellable, PastSellByDate>`. A `Perishable` refuses the sale
   after `SellByDate`; a `NonExpiring` never refuses.
5. **Do not store expiry in `ProductStatus`.** Expiry depends on today's date, so compute it when
   asked. A stored status would go stale. `ProductStatus` stays about the lifecycle.
6. **Do not read the clock inside the model.** Pass `today` (or inject a `TimeProvider`) so each
   date case can be tested without waiting.
7. **Wire the services.** `SellProduct` checks `CanSell` before `Withdraw`; `AddProduct` receives a
   `ShelfLife`; `CatalogService`/`ProductCatalog.GetDisplayLabel` matches on it to show
   "[BEST BEFORE PASSED]" for a `NonExpiring` past its date.

**Self-check:** adding a third kind (say `Frozen`) must only require a new record, one more type in the
`OneOf`, and the compiler pointing at every `Match` that now needs a case. If you had to edit an
existing class hierarchy, you extended by modification instead of composition.


#### What the harness checks

HA4 is verified by seven small Roslyn rules, `HA4.1` to `HA4.7`. They never pin a type name
(except `Product`, `SellProduct`, `CanSell`, `Withdraw`, `Create`/`Build` and `IValidationError`), so you are free to
choose yours. A *value object* here is a `record` / `record struct` that does not implement `IDentifiable`.

| Rule | Checks | Level |
|---|---|---|
| HA4.1 | No class or record has a base type other than `object` (interfaces are fine; `OneOfBase<...>` is caught). | error |
| HA4.2 | No exception is thrown in a constructor (`throw`, `?? throw`, `ThrowIf*`). Return a `OneOf` from a factory instead. | error |
| HA4.3 | `Product` has at least one `OneOf<X, Y, ...>` property with no setter, and every type argument is a value object. | error |
| HA4.4 | At least one value object has a static `Create` or `Build` returning `OneOf<Self, E>`, where `E` implements `IValidationError`. | error |
| HA4.5 | No `is` / `as` / `switch` on a `OneOf` or on its alternatives, and at least one `.Match(...)` over a `OneOf` whose type arguments are all records. | error |
| HA4.6 | The model (entities and value objects) never reads the clock: no `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `TimeProvider.System`. Pass `today` or inject a `TimeProvider`. | error |
| HA4.7 | In `SellProduct`, `CanSell` is called before `Withdraw`. | warning |

`E` implements the interface `IValidationError` (provided in `OmniProduct_CoreDomain.Errors`, with a
generic `ValidationError` record) rather than inheriting from a base class, because HA4.1 forbids inheritance.
A warning is printed as `[WARN]` and does not fail the harness.


### HA4 - Variant

for ADT (OneOf)  , you could also refactor this:

Product.Status (recommended for HA4.3). Today it's a string with three values: "active", "out_of_stock", "deprecated". It's a closed set, which is the case OneOf is for. The strings are compared in ProductLifecycleService.GetActiveProducts and ProductCatalog.GetDisplayLabel, so a typo would compile today.
// ValueObjects/ProductStatus.cs
public record Active;
public record OutOfStock;
public record Deprecated;

// Models/Product.cs
public OneOf<Active, OutOfStock, Deprecated> Status { get; init; } = new Active();
Sell and Deprecate would assign new OutOfStock() or new Deprecated(), relying on OneOf's implicit conversions. The checks against "deprecated" would become Match or IsT2. That also brings HA4.5 into play, which is good practice.
