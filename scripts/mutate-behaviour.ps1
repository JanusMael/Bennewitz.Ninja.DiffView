#!/usr/bin/env pwsh
#Requires -Version 7
# Prove a behaviour test able to fail: break what it guards, check it goes red, and check that a
# change which should not matter leaves it green. scripts/mutate-gates does this for the
# static-analysis gates; this is its sibling for the tests that drive the control.
#   ./scripts/mutate-behaviour.ps1 --list
#   ./scripts/mutate-behaviour.ps1 --only refold
# It refuses to start on a dirty tree, because it reverts with `git checkout`.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/mutate-behaviour.cs -- @Rest
exit $LASTEXITCODE
