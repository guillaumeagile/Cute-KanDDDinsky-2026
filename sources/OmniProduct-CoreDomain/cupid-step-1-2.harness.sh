#!/usr/bin/env bash
# CUPID step 1.2 harness.
#
# Deterministic, non-AI checks for the HA1-HA4 assessment criteria described in
# CUPID-step1-2.md:
#   HA1  - C# idioms          -> built-in Roslyn analyzers via .editorconfig, enforced as build errors
#   HA2  - immutable data     -> Cupid.Harness structural rule (records only, no setters, no mutable collections)
#   HA3  - Records are VOs    -> Cupid.Harness structural rule (records never implement IDentifiable, immutable, ValueObjects holds records only; Price must be one)
#   HA4  - Composable         -> Cupid.Harness rules HA4.1-HA4.7 (no inheritance, no throwing constructors, immutable VOs,
#                                OneOf property on Product, validating factory, Match not is/as/switch, no clock in model;
#                                HA4.7 SellProduct/CanSell/Withdraw order is a [WARN] only and never fails the run)
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
echo " HA2-HA4 - structural checks (Cupid.Harness)"
echo "=============================================="
if dotnet run --project "$HARNESS_PROJECT" -c Release -- --step 1.2 "$TARGET_PROJECT"; then
    echo "[PASS] HA2-HA4"
else
    echo "[FAIL] HA2-HA4"
    overall_status=1
fi

echo
if [ "$overall_status" -eq 0 ]; then
    echo "CUPID step 1.2 harness: ALL CHECKS PASSED"
else
    echo "CUPID step 1.2 harness: FAILED"
fi

exit "$overall_status"
