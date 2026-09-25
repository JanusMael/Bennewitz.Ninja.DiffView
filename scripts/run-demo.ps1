#Requires -Version 7
<#
.SYNOPSIS
Runs the DiffView demo on a pair of files, for looking at the control by hand.

.EXAMPLE
./scripts/run-demo.ps1
./scripts/run-demo.ps1 -Left src/A.cs -Right src/B.cs
./scripts/run-demo.ps1 -Extra '--variant','Dark'
./scripts/run-demo.ps1 -Extra '--viewer'

.NOTES
The demo's own flags are --left, --right, --theme, --variant, --unified, --viewer, --edit, --culture
and --log-level; pass any of them through -Extra.
#>
[CmdletBinding()]
param(
    [string] $Left,
    [string] $Right,
    [string[]] $Extra = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot

# A pair big enough that the overview map has something to show.
if (-not $Left) { $Left = Join-Path $repo 'src/DiffView.Avalonia/SideBySideDiffView.cs' }
if (-not $Right) { $Right = Join-Path $repo 'src/DiffView.Avalonia/InlineDiffView.cs' }

Write-Host "left  $Left"
Write-Host "right $Right"
Write-Host ''
Write-Host 'Things to try:'
Write-Host '  View -> Control               the editor, the unified view or the read-only viewer'
Write-Host '  View -> Show overview map     turns the two-lane map beside the panes on and off'
Write-Host '  drag the box on the map       scrolls continuously; a click anywhere else jumps'
Write-Host '  scroll wheel over the map     scrolls the panes'
Write-Host '  View -> Edit left/right pane  then the copy arrows appear in the number margins;'
Write-Host '                                select some lines for the selection arrow'
Write-Host '  F7 / Shift+F7                 next and previous change;  Ctrl+F  find'
Write-Host ''

$project = Join-Path $repo 'src/DiffView.Demo'
$arguments = @('run', '--project', $project, '--', '--left', $Left, '--right', $Right) + $Extra
& dotnet @arguments
exit $LASTEXITCODE
