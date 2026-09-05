#!/usr/bin/env pwsh
#Requires -Version 7
# Pack ClaudeForge's LayeredEditors.Avalonia.Diagnostics into the local NuGet feed (../nuget-local).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/pack-diagnostics.cs -- @Rest
exit $LASTEXITCODE
