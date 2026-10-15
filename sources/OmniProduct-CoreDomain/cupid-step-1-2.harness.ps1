# CUPID step 1.2 harness (PowerShell version of cupid-step-1-2.harness.sh).
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
Write-Host ' HA2-HA4 - structural checks (Cupid.Harness)'
Write-Host '=============================================='
dotnet run --project $HarnessProject -c Release -- --step 1.2 $TargetProject
if ($LASTEXITCODE -eq 0) {
    Write-Host '[PASS] HA2-HA4'
} else {
    Write-Host '[FAIL] HA2-HA4'
    $overallStatus = 1
}

Write-Host ''
if ($overallStatus -eq 0) {
    Write-Host 'CUPID step 1.2 harness: ALL CHECKS PASSED'
} else {
    Write-Host 'CUPID step 1.2 harness: FAILED'
}

exit $overallStatus
