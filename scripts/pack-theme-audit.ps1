#!/usr/bin/env pwsh
#Requires -Version 7
# Pack the theme-audit dotnet tool (Bennewitz.Ninja.ThemeAudit) into the local NuGet feed (../nuget-local).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/pack-theme-audit.cs -- @Rest
exit $LASTEXITCODE
