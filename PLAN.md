# Plan - CUPID phase 2 harness (branch code/CUPID-phase2, PR #2)

**Resume at:** run the cloud verification (`claude --cloud` from an interactive terminal), then merge decision. Nothing else pending.

## Done
- [x] 2026-10-15 `dfb51e7` AlgebraicDataTypeRule shelved (file + tests, not wired).
- [x] 2026-10-15 wired the rule as HA4.8, relaxed HA4.1 (commits f2fa041 and the following one).
- [x] 2026-10-15 removed HA4.8 again: Program.cs, rule and its tests restored to the `dfb51e7` shelved state; HA4.1 and CUPID-step1-2.md untouched (commit of this PLAN).

## Decisions
- ADTs: C# has no native union yet (not v15); several designs exist (OneOf, record hierarchy). Step 1.2 keeps OneOf only; HA4.1 and the other rules are unchanged.
- HA4.8 caused too many tensions (id clash with HA3, conflict with HA4.1): not wired. The rule stays shelved in `Rules/AlgebraicDataTypeRule.cs` (still declares id "HA3", which clashes with RecordsAreValueObjectsRule if it is ever wired).

## Blocker
- Cloud build/tests not run: `claude --cloud` needs an interactive terminal. Nothing has been compiled or tested for these commits.
