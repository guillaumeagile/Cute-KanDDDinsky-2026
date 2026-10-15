#!/usr/bin/env bash
# CUPID step 1.1 harness.
#
# Deterministic, non-AI checks for the HA1/HA5-HA8, HA12 assessment criteria described in
# CUPID-step1-1.md (Concept 1.1: Single Responsibility -> Unix Philosophy):
#   HA1 - C# idioms            -> built-in Roslyn analyzers via .editorconfig, enforced as build errors
#   HA5 - No god class         -> Cupid.Harness structural rule (max 6 public properties per class)
#   HA6 - Single Concern       -> Cupid.Harness structural rule (public methods span at most 1 bounded-context concern)
#   HA7 - Fan-out              -> Cupid.Harness structural rule (a class touches at most 3 distinct domain types)
#   HA8 - No Persistence in Domain -> Cupid.Harness structural rule (no EF/ORM vocabulary on domain models)
#   HA12 - Rich identifiers    -> Cupid.Harness structural rules (Id/*Id is a Guid/Ulid or a record wrapping one, no NewGuid/Empty; HA12.1 unwrapped primitive id is a [WARN] only)
#
# Exit code is 0 only if every layer passes. Safe to run repeatedly (no state, no network).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TARGET_PROJECT="$SCRIPT_DIR"
HARNESS_PROJECT="$REPO_ROOT/Cupid.Harness"

overall_status=0

echo "=============================================="
echo " HA1 - C# idioms (Roslyn analyzers, build as errors)"
echo "=============================================="
# Only the IDE/style analyzer diagnostics are promoted to errors (via .editorconfig severities),
# so NuGet advisory warnings (NU*) or other unrelated build warnings never fail this gate.
if dotnet build "$TARGET_PROJECT" /nologo /p:EnforceCodeStyleInBuild=true /p:AnalysisMode=AllEnabledByDefault /p:TreatWarningsAsErrors=false; then
    echo "[PASS] HA1"
else
    echo "[FAIL] HA1"
    overall_status=1
fi

echo
echo "=============================================="
echo " HA5-HA8, HA12 - structural checks (Cupid.Harness)"
echo "=============================================="
if dotnet run --project "$HARNESS_PROJECT" -c Release -- --step 1.1 "$TARGET_PROJECT"; then
    echo "[PASS] HA5-HA8, HA12"
else
    echo "[FAIL] HA5-HA8, HA12"
    overall_status=1
fi

echo
if [ "$overall_status" -eq 0 ]; then
    echo "CUPID step 1.1 harness: ALL CHECKS PASSED"
else
    echo "CUPID step 1.1 harness: FAILED"
fi

exit "$overall_status"
