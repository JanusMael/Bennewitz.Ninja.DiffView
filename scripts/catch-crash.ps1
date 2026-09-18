#!/usr/bin/env pwsh
#Requires -Version 7
# Re-run the test suite until it crashes, keeping the output of the run that did. The test host
# aborts intermittently and the summary then reads "Failed!" with "failed: 0" and a short total,
# which any grep reads as success — so this judges the whole summary, not one line.
# Pass --check <log> to judge a captured log instead of running anything.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/catch-crash.cs -- @Rest
exit $LASTEXITCODE
