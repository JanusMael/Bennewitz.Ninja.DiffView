#Requires -Version 7
<#
.SYNOPSIS
The interactive half of the by-hand demo pass open item 9 owes, scripted start to finish so the
desktop is borrowed once and given straight back.

.DESCRIPTION
scripts/drive-demo captures what a window looks like; this drives what it DOES. Everything here
needs the foreground, which is why none of it could go in the capture half: a menu popup will not
open reliably without it, tooltip dwell needs real pointer motion, and the focus accent needs focus.
The pointer it moves is YOURS -- that is the whole reason this is attended.

Run it, look away, and take the desktop back when it prints DONE. It closes the demo itself. If a
step fails it records the failure and carries on, so one bad step does not cost the whole pass, and
every failure is listed at the end rather than buried.

⛔ It will refuse rather than misfire: the driver checks that each click would land on the demo and
that the demo holds the keyboard before it types, so if another window steals the foreground mid-run
the steps stop with a reason instead of clicking into whatever is there. Nothing here raises the
demo over your work -- AGENTS.md section 9 on why that rule cuts both ways.

.NOTES
⛔ WINDOWS ONLY, and for the same reason scripts/drive-demo-uia.ps1 is a script of its own: this
pass addresses parts by AutomationId -- the panes, the two margins and the palette entry -- and the
X11 back end refuses that outright, having no accessibility bridge, because plan 00023 leaves AT-SPI
out. A Linux counterpart would have to give a coordinate for every one of those parts, which is the
brittleness the `--id` spelling exists to remove. So there is deliberately no .sh beside this file;
scripts/drive-demo itself runs on both platforms and is where the portable half lives.

⛔ THE PROGRESS LINES GO TO Write-Host, NEVER Write-Output. A PowerShell function returns everything
written to its output stream, not just the value after `return`, so one Write-Output inside `Drive`
makes `if (Drive …)` true whenever the function printed anything -- which is to say always. The
first run of this pass (2026-10-08) had exactly that defect: every guarded block ran whether its
step had succeeded or not, and it produced a frame named for the colour-blind palette that was the
demo with a menu open over it. `DriveDemoTests` holds the rule now. The [OutputType] attributes
below are documentation and are NOT what fixes it.

Ordering is deliberate: anything that changes what a later frame shows comes late -- the splitter
drag, then the map drag, then the palette, which repaints everything.

The change-marker margin's tooltip exists only for a CHANGED line, so F7 runs before it: the current
block is centred by the centring scroll (AGENTS.md section 6), which puts a changed row under the
margin's middle. A ladder either side of centre is the fallback.

The drag coordinates are client-relative to the demo's 1100x720 window and come from UI Automation
bounds: the connector gutter spans client x 531..547 (so 539 is its middle) and the overview map
x 1078..1100 (so 1089 is its middle). If the window is ever not 1100x720 these move, and the driver
refuses a drag that would start or end outside the window rather than guessing.

⛔ KNOWN, MEASURED 2026-10-08: THE SPLITTER DRAG REPORTS SUCCESS AND MOVES NOTHING. The press, the
walk and the release all take, so the driver has nothing to refuse — and the panes stay where they
were. The cause is the y, not the verb: the map drag below it moved 65% of the frame on the same
run, and a before/after comparison of the splitter pair found 48 pixels moved, a 3x16 block that is
the caret blinking. At y=364 the connector column holds a POLYGON, and AGENTS.md section 6 is
explicit that a press on a polygon selects that block where a press on the empty column drags the
splitter. So this step currently measures block selection. The empty column is scarce on a pair with
429 changes, which is itself worth a look: pick a y the frame shows as empty, and verify the panes
actually moved rather than trusting the `ok`.

.EXAMPLE
pwsh -NoProfile -File scripts/drive-demo-interactive.ps1 -Out ../scratch/item9-interactive
#>
[CmdletBinding()]
param(
    # Discovered, never assumed: this file's own directory is in the checkout, so the checkout is
    # its parent. DriveDemoTests fails a script that writes a repository path in.
    [string] $Repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [Parameter(Mandatory = $true)][string] $Out
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path
Set-Location $Repo

$left = Join-Path $Repo 'src/DiffView.Avalonia/SideBySideDiffView.cs'
$right = Join-Path $Repo 'src/DiffView.Avalonia/InlineDiffView.cs'
$uia = Join-Path $PSScriptRoot 'drive-demo-uia.ps1'

$failures = [System.Collections.Generic.List[string]]::new()
$script:step = 0
$script:lastOutput = @()

function Drive {
    <#  One driver invocation. Records a failure and returns false rather than throwing, so the
        pass continues; the driver's own stderr is what says why. The driver's output is handed
        back out of band, in $script:lastOutput, for the one caller that parses it.  #>
    [OutputType([bool])]
    param([Parameter(Mandatory = $true)][string[]] $Verbs, [string] $What)

    $arguments = @('run', 'scripts/drive-demo.cs', '--') + $Verbs
    # @() at the assignment: a pipeline yielding one line yields it as a SCALAR, and the .Count
    # below then fails under Set-StrictMode.
    $output = @(& dotnet @arguments 2>&1)
    $ok = $LASTEXITCODE -eq 0
    $script:lastOutput = $output
    Write-Host (($ok ? '  ok   ' : '  FAIL ') + $What)
    foreach ($line in $output) { Write-Host ('         ' + $line) }
    if (-not $ok) { $failures.Add($What + ' :: ' + ($output -join ' | ')) }
    return $ok
}

function Shot {
    [OutputType([bool])]
    param([Parameter(Mandatory = $true)][string] $Name, [string] $Window = 'demo')
    $script:step++
    $file = Join-Path $Out ('{0:d2}-{1}.png' -f $script:step, $Name)
    return (Drive -Verbs @('capture', $Window, $file) -What ('capture ' + $Window + ' -> ' + (Split-Path $file -Leaf)))
}

function Focus {
    <#  Put the keyboard in a pane so a chord lands. The driver will not type unless the demo holds
        the keyboard, and will not click through a popup lying over its target, so this doubles as
        the check that the previous section cleaned up after itself.  #>
    [OutputType([bool])]
    param([string] $Pane = 'SideBySide/LeftPane')
    return (Drive -Verbs @('click', 'left', '--id', $Pane) -What ('focus ' + $Pane))
}

function ElementRect {
    <#  A part's bounds in absolute physical pixels, read through the committed UIA reader, which
        prints path, control type, x, y, width, height, hwnd -- tab separated. A hashtable and not
        an array, because @(@(…), @(…)) FLATTENS in PowerShell.  #>
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][int] $DemoPid)

    $raw = @(& pwsh -NoProfile -File $uia -DemoProcessId $DemoPid -Path $Path 2>&1)
    if ($LASTEXITCODE -ne 0 -or $raw.Count -eq 0) {
        Write-Host ('  FAIL could not read the bounds of ' + $Path + ': ' + ($raw -join ' | '))
        return $null
    }

    $fields = @($raw[0] -split "`t")
    if ($fields.Count -lt 6) {
        Write-Host ('  FAIL the reader said something unexpected for ' + $Path + ': ' + $raw[0])
        return $null
    }

    return @{ X = [int] $fields[2]; Y = [int] $fields[3]; W = [int] $fields[4]; H = [int] $fields[5] }
}

function ClientOrigin {
    <#  The demo client area's top-left in screen coordinates, so a part's absolute bounds can be
        turned into the client-relative coordinates every verb takes. `geometry demo` prints
        `demo <id> <W>x<H>+<X>+<Y> viewable`.  #>
    if (-not (Drive -Verbs @('geometry', 'demo') -What 'read the demo window geometry')) { return $null }

    $line = $script:lastOutput | Where-Object { $_ -match '^demo\s' } | Select-Object -First 1
    if (-not $line) { return $null }
    if ($line -notmatch '\+(-?\d+)\+(-?\d+)') { return $null }
    return @{ X = [int] $Matches[1]; Y = [int] $Matches[2] }
}

Write-Host '=== launching; the demo takes the foreground from here until DONE ==='
$report = @(& dotnet run scripts/drive-demo.cs -- launch --left $left --right $right --edit both 2>&1)
Write-Host ($report -join "`n")
$line = $report | Where-Object { $_ -match 'launched pid=(\d+)' } | Select-Object -First 1
if (-not $line) { throw 'the demo did not launch; nothing else can run' }
$demoPid = [int] ($line -replace '.*launched pid=(\d+).*', '$1')
Start-Sleep -Seconds 3

try {
    # ---- the focus accent, which is a collapsed overlay in the header and must move no row ----
    Write-Host '=== focus accent ==='
    Focus 'SideBySide/LeftPane' | Out-Null
    Start-Sleep -Milliseconds 600
    Shot 'focus-left' | Out-Null
    Focus 'SideBySide/RightPane' | Out-Null
    Start-Sleep -Milliseconds 600
    Shot 'focus-right' | Out-Null

    # ---- the pane's own context menu at real DPI, and light-dismiss ----
    Write-Host '=== pane context menu ==='
    if (Drive -Verbs @('mark', 'then', 'click', 'right', '--id', 'SideBySide/LeftPane', 'then', 'popup') -What 'right-click the left pane') {
        Shot 'pane-menu' 'popup' | Out-Null
    }

    Drive -Verbs @('key', 'Escape') -What 'Escape to light-dismiss the menu' | Out-Null
    Start-Sleep -Milliseconds 800
    Shot 'after-light-dismiss' | Out-Null

    # ---- the line-number margin's tooltip: dwell, placement, legibility at real size ----
    Write-Host '=== line-number tooltip ==='
    if (Drive -Verbs @('hover', '--id', 'SideBySide/LeftPane/LineNumbers') -What 'hover the line-number margin') {
        Shot 'tooltip-line-numbers' 'popup' | Out-Null
    }

    # ---- the demo's View menu, which AGENTS.md section 9 says is taller than its popup ----
    # Clicked by coordinate because the menu-bar headers carry a Name and no x:Name, so nothing
    # derives an AutomationId for them -- unlike their entries, which all have one.
    Write-Host '=== the View menu at real DPI ==='
    if (Drive -Verbs @('mark', 'then', 'click', 'left', '96', '17', 'then', 'popup') -What 'open the View menu') {
        Shot 'view-menu' 'popup' | Out-Null
    }

    # Closed explicitly, whether or not it opened: a popup left over the pane is what refused the
    # first run's next click, and every step after it cascaded.
    Drive -Verbs @('key', 'Escape') -What 'Escape to close the View menu' | Out-Null
    Start-Sleep -Milliseconds 800

    # ---- navigation and the current-change border at real size ----
    Write-Host '=== F7 navigation ==='
    Focus | Out-Null
    Drive -Verbs @('key', 'F7') -What 'F7 once' | Out-Null
    Drive -Verbs @('key', 'F7') -What 'F7 twice' | Out-Null
    Start-Sleep -Milliseconds 800
    Shot 'after-f7-twice' | Out-Null

    # ---- the change-marker margin's tooltip, which exists only for a CHANGED line ----
    Write-Host '=== change-marker tooltip, on a changed row ==='
    $markerShown = $false
    if (Drive -Verbs @('hover', '--id', 'SideBySide/LeftPane/ChangeMarkers') -What 'hover the change-marker margin at its centre') {
        $markerShown = Shot 'tooltip-change-markers' 'popup'
    }

    if (-not $markerShown) {
        # A row is about 16 px at 96 DPI here, so these are the eight rows either side of centre.
        $rect = ElementRect -Path 'SideBySide/LeftPane/ChangeMarkers' -DemoPid $demoPid
        $origin = ClientOrigin
        if ($null -ne $rect -and $null -ne $origin) {
            $cx = $rect.X - $origin.X + [int] ($rect.W / 2)
            $cy = $rect.Y - $origin.Y + [int] ($rect.H / 2)
            Write-Host ('       the margin centre is client ' + $cx + ',' + $cy + '; laddering either side')
            foreach ($dy in @(-16, 16, -32, 32, -48, 48, -64, 64)) {
                $at = $cy + $dy
                if (Drive -Verbs @('hover', [string] $cx, [string] $at) -What ('hover the change-marker margin at client ' + $cx + ',' + $at)) {
                    $markerShown = Shot 'tooltip-change-markers' 'popup'
                    break
                }
            }
        }

        if (-not $markerShown) {
            $failures.Add('change-marker tooltip :: no changed row under the pointer at the margin centre or eight rows either side')
        }
    }

    # ---- the find bar, which is hidden until asked for ----
    Write-Host '=== the find bar ==='
    Focus | Out-Null
    Drive -Verbs @('key', 'ctrl+f') -What 'Ctrl+F to open the find bar' | Out-Null
    Start-Sleep -Seconds 1
    Shot 'find-bar' | Out-Null
    Drive -Verbs @('key', 'Escape') -What 'Escape to close the find bar' | Out-Null
    Start-Sleep -Milliseconds 600

    # ---- the splitter, which is the connector gutter's empty column ----
    # AGENTS.md section 6: the headers and the panes share a column layout, so a header must stay
    # exactly as wide as its pane AND start where it starts. That contract was broken for four
    # plans by two stale spacer numbers that summed to the same total, which no headless test
    # caught. A frame after a drag is where it shows.
    Write-Host '=== splitter drag ==='
    Shot 'split-before' | Out-Null
    if (Drive -Verbs @('drag', 'left', '539', '364', 'to', '689', '364') -What 'drag the splitter 150px right') {
        Start-Sleep -Milliseconds 800
        Shot 'split-after' | Out-Null
    }

    # ---- the overview map's viewport box, where a press inside drags and a press outside jumps ----
    Write-Host '=== overview map drag ==='
    if (Drive -Verbs @('drag', 'left', '1089', '90', 'to', '1089', '420') -What 'drag the map viewport down') {
        Start-Sleep -Milliseconds 800
        Shot 'map-dragged' | Out-Null
    }

    # ---- the colour-blind palette, which the capture half cannot reach: there is no flag for it ----
    # LAST, because it repaints everything. Its entry has x:Name="ColourBlindPalette" and Avalonia
    # derives an AutomationId from a Name, so it is clickable by id inside the open popup rather
    # than by counting rows down a menu.
    Write-Host '=== colour-blind palette ==='
    if (Drive -Verbs @('mark', 'then', 'click', 'left', '96', '17', 'then', 'popup') -What 'open the View menu for the palette') {
        if (Drive -Verbs @('click', 'left', '--id', 'ColourBlindPalette') -What 'tick Colour-blind palette') {
            Start-Sleep -Seconds 1
            Shot 'palette-colour-blind' | Out-Null
        }
    }
    else {
        $failures.Add('colour-blind palette :: the View menu never opened, so its entry was unreachable')
    }
}
finally {
    $p = Get-Process -Id $demoPid -ErrorAction SilentlyContinue
    if ($p) {
        $null = $p.CloseMainWindow()
        Start-Sleep -Milliseconds 1500
        $p.Refresh()
        if (-not $p.HasExited) { Stop-Process -Id $demoPid -Force }
    }

    Write-Host ''
    Write-Host ('captured ' + (Get-ChildItem $Out -Filter *.png -ErrorAction SilentlyContinue).Count + ' frames into ' + $Out)
    if ($failures.Count -eq 0) {
        Write-Host 'every step took'
    }
    else {
        Write-Host ('STEPS THAT DID NOT TAKE (' + $failures.Count + '):')
        foreach ($f in $failures) { Write-Host ('  - ' + $f) }
    }

    Write-Host ''
    Write-Host 'Every frame is gated on its own step, so a frame that exists is one whose step'
    Write-Host 'reported success. Judge them against the failure list above, not their names alone.'
    Write-Host ''
    Write-Host 'STILL NOT COVERED anywhere, and owed:'
    Write-Host '  macOS -- plan 00023 specifies that back end and deliberately does not write it,'
    Write-Host '  because its Accessibility and Screen Recording grants are interactive.'
    Write-Host ''
    Write-Host 'DONE -- the desktop is yours again.'
}
