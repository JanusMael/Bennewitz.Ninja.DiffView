#!/usr/bin/env pwsh
#Requires -Version 7
# Regenerate docs/locale-review/<culture>.md, the documents a native speaker reviews a locale from.
# Pass a culture name to do just that one; pass --check to report staleness instead of rewriting
# (exit 1 when one differs).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/gen-locale-review.cs -- @Rest
exit $LASTEXITCODE
