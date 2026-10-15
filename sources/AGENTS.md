# AGENTS.md

## What this solution is

This is a C# solution (`CuteKanDDDsky-workshop.sln`) built for a DDD/CUPID workshop. The
overall vision and narrative for the workshop is in `Workshop.md` — read that first to
understand *why* the exercises exist (legacy anemic model, strategic DDD changes, tactical
CUPID refactors).

The solution is split into several projects, each with a distinct role:

- **`OmniProduct-CoreDomain/`** — the exercise. This is the legacy-ish domain code
  (`Product`, `ProductService`, etc.) that participants refactor to satisfy the goals
  described in:
  - `OmniProduct-CoreDomain/CUPID-step1-1.md` — Single Responsibility / Unix philosophy:
    split `ProductService`/`Product` into per-bounded-context concerns (Catalog, Pricing,
    Storage, Supplier, Sales, Lifecycle, Notification, Transport).
  - `OmniProduct-CoreDomain/CUPID-step1-2.md` — Composable/immutable/ADT refactor: extract
    VAT out of `Price`, make it composable (Margin → TransportationFee → VAT), enforce
    immutable records and algebraic data types, remove inheritance.

- **`OmniProduct-Core.Test/`** — unit tests for the domain code above (`ProductServiceTests`,
  `StoredProductTests`).

- **`Cupid.Harness/`** — the Roslyn-analyzer-based grading harness. Deterministic,
  non-AI-guided: same code in, same pass/fail out. Structural rules (no god class,
  single-concern, fan-out limit, no persistence in domain, no inheritance, immutability,
  ADT shape, ...) live in `Cupid.Harness/Rules/`, driven by
  `Cupid.Harness/ubiquitous-language-map.json` for the bounded-context vocabulary.

- **`Cupid.Harness.Test/`** — pinning tests for the harness rules themselves (one test file
  per rule, e.g. `SingleConcernRuleTests.cs`, `FanOutRuleTests.cs`). Change these only when
  intentionally changing a rule's behavior.

## How the exercise is assessed

Each step ships its own shell script under `OmniProduct-CoreDomain/`, run from that
directory:

```
./cupid-step-1-1.harness.sh   # HA1, HA5-HA12: idioms, god class, single concern, fan-out, no persistence in domain, entities implement IDentifiable, no services in entity ctors, records are Value Objects, rich ULID/UUIDv7 ids (HA12.1 is a [WARN])
./cupid-step-1-2.harness.sh   # HA1-HA4: idioms, immutability, value objects, composable (HA3, HA4.1-HA4.7: no inheritance, no throwing ctors, VOs, OneOf/Match, no clock; HA4.7 is a [WARN])
```

These build the exercise project with Roslyn analyzers promoted to errors, then run
`Cupid.Harness` against it. Exit code 0 only if every check passes for that step.

## Guidance for working in this repo

- Don't "fix" the exercise code in `OmniProduct-CoreDomain/` preemptively — the point is
  for a participant (or an agent standing in for one) to refactor it step by step against
  the harness. When asked to do the exercise, work from the step's `.md` file and validate
  with that step's harness script.
- Don't relax or rewrite `Cupid.Harness/Rules/*` or their pinning tests in
  `Cupid.Harness.Test/` to make the exercise pass — that defeats the assessment. If a rule
  genuinely looks wrong, flag it rather than quietly loosening it.
- This project has a knowledge graph (code-review-graph MCP) — prefer its tools over
  Grep/Glob/Read when exploring code (see the project's `CLAUDE.md` for details).
