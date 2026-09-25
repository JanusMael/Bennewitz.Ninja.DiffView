#!/usr/bin/env pwsh
#Requires -Version 7
# Prove that the static-analysis gates can fail: plant each defect, or blind each gate, and check
# that a named test or the build kills it. A surviving mutation means that gate reports clean over a
# codebase that is not, which reads as coverage and is indistinguishable from success.
# ⛔ Reverts src and tests before every mutation, so it refuses to start on a dirty tree. Commit
# first; the commit is the revert point. Pass --list to name them, --only <text> for a subset.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/mutate-gates.cs -- @Rest
exit $LASTEXITCODE
