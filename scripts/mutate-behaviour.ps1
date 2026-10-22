#!/usr/bin/env pwsh
#Requires -Version 7
# Prove a behaviour test able to fail: break what it guards, check it goes red — by the test the
# mutation names, in the words it names — and check that a change which should not matter leaves it
# green. scripts/mutate-gates does this for the static-analysis gates; this is its sibling for the
# tests that drive the code.
#   ./scripts/mutate-behaviour.ps1 --list
#   ./scripts/mutate-behaviour.ps1 --only refold
#   ./scripts/mutate-behaviour.ps1 --set tests/AgentBaseline.Tests/Mutations/section-decisions.json --check
#   ./scripts/mutate-behaviour.ps1 --parallel 4 --owed HEAD
# Each edit is reverted from a snapshot, so it runs over uncommitted work; edit nothing it compiles
# while it runs. --parallel runs in worktrees of its own, uncommitted work included, and never edits
# this checkout; --owed HEAD runs only the sets a change since HEAD touches.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/mutate-behaviour.cs -- @Rest
exit $LASTEXITCODE
