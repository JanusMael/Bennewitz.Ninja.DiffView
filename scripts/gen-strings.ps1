#!/usr/bin/env pwsh
#Requires -Version 7
# Regenerate src/DiffView.Avalonia/Localization/Strings.resx from DiffViewStrings.EnglishDefaults.
# Pass --check to report staleness instead of rewriting (exit 1 when it differs).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/gen-strings.cs -- @Rest
exit $LASTEXITCODE
