#!/usr/bin/env pwsh
#Requires -Version 7
# Drive the running demo for a by-hand pass: launch it detached, find its window and the popups it
# opens, click and type into it, capture pixels. Verbs chain with `then`:
#   scripts/drive-demo.ps1 launch then mark then click left 88 16 then popup then capture popup view.png
# Only the X11 back end exists; AGENTS.md §9 is the prose its verbs replace.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/drive-demo.cs -- @Rest
exit $LASTEXITCODE
