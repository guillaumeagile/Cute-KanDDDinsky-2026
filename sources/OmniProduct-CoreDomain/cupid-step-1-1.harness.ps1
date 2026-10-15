# CUPID step 1.1 harness (PowerShell version of cupid-step-1-1.harness.sh).
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

$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$RepoRoot = Split-Path -Parent $ScriptDir
$TargetProject = $ScriptDir
$HarnessProject = Join-Path $RepoRoot 'Cupid.Harness'

$overallStatus = 0

Write-Host '=============================================='
Write-Host ' HA1 - C# idioms (Roslyn analyzers, build as errors)'
Write-Host '=============================================='
# Only the IDE/style analyzer diagnostics are promoted to errors (via .editorconfig severities),
# so NuGet advisory warnings (NU*) or other unrelated build warnings never fail this gate.
dotnet build $TargetProject /nologo /p:EnforceCodeStyleInBuild=true /p:AnalysisMode=AllEnabledByDefault /p:TreatWarningsAsErrors=false
if ($LASTEXITCODE -eq 0) {
    Write-Host '[PASS] HA1'
} else {
    Write-Host '[FAIL] HA1'
    $overallStatus = 1
}

Write-Host ''
Write-Host '=============================================='
Write-Host ' HA5-HA8, HA12 - structural checks (Cupid.Harness)'
Write-Host '=============================================='
dotnet run --project $HarnessProject -c Release -- --step 1.1 $TargetProject
if ($LASTEXITCODE -eq 0) {
    Write-Host '[PASS] HA5-HA8, HA12'
} else {
    Write-Host '[FAIL] HA5-HA8, HA12'
    $overallStatus = 1
}

Write-Host ''
if ($overallStatus -eq 0) {
    Write-Host 'CUPID step 1.1 harness: ALL CHECKS PASSED'
} else {
    Write-Host 'CUPID step 1.1 harness: FAILED'
}

exit $overallStatus
