#!/usr/bin/env pwsh
#Requires -Version 7
# Drive the running demo for a by-hand pass: launch it detached, find its window and the popups it
# opens, click and type into it, capture pixels. Verbs chain with `then`:
#   scripts/drive-demo.ps1 launch then mark then click left 88 16 then popup then capture popup view.png
# On Windows a part can be addressed by its AutomationId instead of a coordinate:
#   scripts/drive-demo.ps1 click left --id SideBySide/LeftPane/LineNumbers
# The X11 and Windows back ends exist; macOS is specified in plan 00023 and not written.
# AGENTS.md §9 is the prose these verbs replace.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
& dotnet run scripts/drive-demo.cs -- @Rest
exit $LASTEXITCODE
