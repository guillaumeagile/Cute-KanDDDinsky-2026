# You use CUPID because you want to be CUTE
### A DDD workshop in C# — Guillaume & Sam

--

# Why are we here?
- Take a legacy domain that violates good design habits
- Refactor it step by step, with a harness to check each step
- Leave with a model that tells the business story

--

# The legacy code
- Spring Boot + JPA origin, rewritten in C#
- `Product` does everything: catalog, pricing, stock, suppliers, transport
- `ProductService` orchestrates all of it
- Models and services depend on each other through the database

---

# CUTE DDD
### The intent

--

## **C** — Contextual
Design decisions follow the specific business context

--

## **U** — Ubiquitous
Shared language understood by developers and stakeholders alike

--

## **T** — Testable
Easy to test at every level: unit, integration, acceptance

--

## **E** — Expressive
The code clearly communicates business intent

---

# CUPID
### The realisation of CUTE

| CUPID property | What it means | CUTE letter | How the harness checks it |
|---|---|---|---|
| **C**omposable | Plays well with others | **T**estable | No inheritance: behaviour is extended by composing small parts |
| **U**nix philosophy | Does one thing well | **C**ontextual | No god class, single concern per class |
| **P**redictable | Does what you would expect | **T**estable | Immutable data, no setters: same input, same output |
| **I**diomatic | Feels natural | **E**xpressive | C# idioms, record-based algebraic types |
| **D**omain-based | Models the problem domain in language and structure | **U**biquitous | Entities by identity, services kept out of entities, domain vocabulary map |

--

# You use CUPID because you want to be CUTE

---

# Strategic: find the contexts

--

## Feature requests that break the legacy model
- multiple suppliers per region
- reseller price = supplier price + margin
  - margin is per region
  - VAT is added only on the margin
- slug depends on the frontend implementation
- frontend asks for catalog-specific data, images for example
- frontend must not display all product fields

--

## Tackle complexity: Event Storming
- Put the domain events on a wall, in business language
- Look for where the vocabulary changes: that is a boundary
- Name each area: **bounded contexts**

--

## Domain Storytelling + Context Mapping
Write stories about Products, Suppliers and Stocks:
- a supplier adds a product to the catalog, then ships it into the stock
- a product is in stock, then sold: the supplier is advised, the stock is updated
- a product is in stock but deprecated: the supplier is advised, the stock is updated, the customer sees the label change

**Goal**: split the domain into bounded contexts and let the events emerge

--

## Applying CUTE strategically
1. **Contextual**: align code structure with the domain boundaries
2. **Ubiquitous**: use domain terms in code, bridge tech and business
3. **Testable**: design for testability; respect the test pyramid, start with unit tests
4. **Expressive**: intent-revealing names, self-documenting structure

---

# Tactical: make the code easier to maintain

--

## Technical concerns
- Nulls and exceptions
- Mapping problem: exposing entities instead of dedicated DTOs
- Anemic domain model
- Too much logic in constructors: extract validation
- Slug logic coupled to a slug library: invert the dependency
- ORM is heavy: use DTOs
- Ports and adapters for persistence

--

## Modelling tools
- **Primitive obsession**: wrap primitives in value objects
- **Aggregates, entities, value objects**: know which is which
- **Events**: make state changes explicit
- **Validations vs invariants**: validate at the edge, protect invariants in the model
- **Always-valid model**: an object cannot exist in an invalid state
- **Make illegal states unrepresentable**: use types to rule out bad combinations

---

# Practice 1: Single responsibility → Unix philosophy
### Step 1

--

## Problem
`ProductService` handles catalog, pricing, stock, supplier notification and transport, all at once.

## Exercise
- Extract the smallest `Product` entity that fits the **Storage** context
- Keep only the properties useful to stock a product in a warehouse
- Do the same for catalog, pricing, supplier notification and transport
- Name each class with domain vocabulary: `Catalog`, not `ProductCatalogManager`

--

## Assessment
```bash
cd OmniProduct-CoreDomain
./cupid-step-1-1.harness.sh
```

Checks: idioms, no god class, single concern per class, entity fan-out, no persistence in the domain, entities implement `IDentifiable`, no services in entity constructors.

---

# Practice 2: Composable, immutable, algebraic
### Step 2

--

## Problem

check the branch `CUPID-step-2`


--

## Assessment
```bash
cd OmniProduct-CoreDomain
./cupid-step-1-2.harness.sh
```

Checks: idioms, immutable data, algebraic data types as record hierarchies, no inheritance, records are value objects and never entities.

---

# Practice 3: Integration
- Create your own rules from what you learned
- Apply them to your own codebase
- Bring one domain concept from your code into a bounded context

---

# Conclusions

--

## Learning harvest
- What surprised you?
- What resonates with your daily code?

## Before / after
- What improved most?
- Which change was hardest?

## Action plan
- Tomorrow, this week, this month

--

## SOLID or CUTE?
- SOLID: good habits for object-oriented code
- CUPID: the realisation of CUTE for modern, functional-leaning C#
- Functional programming + CUTE = a model that reads like the business

### You use CUPID because you want to be CUTE.

---

# Thank you!

--

## Questions?

---

# References

[CUPID: for joyful coding](https://dannorth.net/blog/cupid-for-joyful-coding/)

[CUPID, the backstory](https://dannorth.net/blog/cupid-the-back-story/)

[SOLID](https://en.wikipedia.org/wiki/SOLID)

[Union Types in C#](https://www.youtube.com/watch?v=PR_kh6lA0aQ)

[More on Union Types](https://codingbolt.net/2026/09/22/c-15-union-types/)
