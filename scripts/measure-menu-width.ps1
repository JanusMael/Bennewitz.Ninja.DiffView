#!/usr/bin/env pwsh
#Requires -Version 7
# How wide each shipped locale's menu entries are against English, in display columns — a CJK glyph
# takes two — so a by-hand pass in a locale knows what to look at first. Pass --columns <text> for
# one text's width.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/measure-menu-width.cs -- @Rest
exit $LASTEXITCODE
