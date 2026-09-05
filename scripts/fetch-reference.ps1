#!/usr/bin/env pwsh
#Requires -Version 7
# Fetch the read-only upstream checkouts listed in reference/sources.json.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/fetch-reference.cs -- @Rest
exit $LASTEXITCODE
