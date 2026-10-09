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

⛔ ASK BEFORE RUNNING THIS WHEN SOMEONE IS AT THE MACHINE. The refusals above stop it doing the wrong
thing; they do not stop it taking the desktop you are using. This pass launches a window, moves your
pointer and presses your keys, and on 2026-10-09 it did exactly that to someone mid-sentence. The
capture half of a by-hand pass needs none of that -- PrintWindow draws a window that is behind
others -- so if what you want is frames rather than gestures, do not run this.

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

⛔ A DRAG THAT REPORTS SUCCESS HAS NOT NECESSARILY MOVED ANYTHING, and the splitter is where that
bit. Measured 2026-10-08: the step reported ok while the panes stayed put, because at the scroll
position F7 left behind, y=364 was inside a connector POLYGON -- and AGENTS.md section 6 is explicit
that a press on a polygon selects that block where a press on the empty column drags the splitter.
The press, the walk and the release each took, so the driver had nothing to refuse. The map drag
moved 65% of the frame on the same run and a before/after comparison of the splitter pair found 48
pixels, a 3x16 block that is the caret blinking, which is how the verb was cleared and the
coordinate blamed. The step now scrolls to the top first, where the column is empty, and CHECKS THE
PANE'S WIDTH either side rather than trusting the `ok`. ⚠ The empty column is scarce on a pair with
429 changes, which is worth a judgement of its own.

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
$unread = [System.Collections.Generic.List[string]]::new()
$script:step = 0
$script:lastOutput = @()
$script:logFile = $null

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

function ReadProbe {
    <#  One `probe` line, read into its parts. `absent` is an ANSWER: a part shown only on request is
        genuinely gone from the automation tree while it is off.  #>
    [OutputType([hashtable])]
    param([Parameter(Mandatory = $true)][string] $Line)

    if ($Line -like '* absent') { return @{ Found = $false; Focused = $false; Range = $null } }

    # Tokens rather than a regex for `focused`: `unfocused` CONTAINS `focused`, and a pattern that
    # gets that boundary subtly right is one the next reader has to verify before trusting.
    $tokens = @($Line -split '\s+')

    $range = $null
    $rangeToken = @($tokens | Where-Object { $_ -like 'range=*' }) | Select-Object -First 1
    if ($rangeToken) {
        # ⛔ InvariantCulture. The driver writes the number with a dot whatever the machine's culture,
        # and Parse without this reads 41.375 as 41375 on a German desk.
        $range = [double]::Parse(
            $rangeToken.Substring('range='.Length),
            [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture)
    }

    return @{ Found = $true; Focused = ($tokens -contains 'focused'); Range = $range }
}

function Probe {
    <#  What the driver can say about one part. A probe that could not run at all is not an answer,
        so it reports Read = $false and the caller records the reading as unread rather than failed.  #>
    [OutputType([hashtable])]
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Drive -Verbs @('probe', $Path) -What ('probe ' + $Path))) {
        return @{ Read = $false; Found = $false; Focused = $false; Range = $null }
    }

    $line = $script:lastOutput | Where-Object { $_ -like 'probe *' } | Select-Object -First 1
    if (-not $line) { return @{ Read = $false; Found = $false; Focused = $false; Range = $null } }

    $parts = ReadProbe -Line $line
    return @{ Read = $true; Found = $parts.Found; Focused = $parts.Focused; Range = $parts.Range }
}

function Assert {
    <#  One assertion. ⛔ This is what the pass is FOR: before plan 00033 every step was judged by
        whether the driver verb threw, so a splitter drag that moved nothing and a click that was
        refused both reported ok.  #>
    param([Parameter(Mandatory = $true)][string] $What, [Parameter(Mandatory = $true)][bool] $Holds, [string] $Detail)

    if ($Holds) { Write-Host ('  ✓ ' + $What) ; return }
    Write-Host ('  ✗ ' + $What + ' -- ' + $Detail)
    $failures.Add('ASSERTION ' + $What + ' :: ' + $Detail)
}

function AssertFocus {
    <#  That $Holder has the keyboard and $Other does not.

        ⛔ NEITHER having it is a THIRD outcome and is not the application's fault. The keyboard
        belongs to one window on the desktop, so a person coming back to their machine mid-pass takes
        it — and a pass that called that a failed assertion would blame the control for something a
        human did. It is reported unread instead, which is the honest answer and also the one that
        says what to do about it. Measured 2026-10-09: this pass borrowed the desktop while its owner
        was working, and the first focus assertion was the casualty.  #>
    param(
        [Parameter(Mandatory = $true)][string] $What,
        [Parameter(Mandatory = $true)][string] $Holder,
        [Parameter(Mandatory = $true)][string] $Other)

    $holder = Probe $Holder
    $other = Probe $Other

    if (-not $holder.Read -or -not $other.Read) {
        Unread $What 'a pane could not be probed'
        return
    }

    if (-not $holder.Focused -and -not $other.Focused) {
        Unread $What 'neither pane holds the keyboard, so something outside the demo has it — the pass was interrupted'
        return
    }

    Assert $What ($holder.Focused -and -not $other.Focused) `
        ($Holder + ' focused=' + $holder.Focused + ', ' + $Other + ' focused=' + $other.Focused)
}

function Unread {
    <#  A reading that could not be taken at all — the probe failed, not the property. Named in the
        summary rather than counted as a pass, because a run reporting twelve of twelve while some of
        them read nothing is the same lie in a smaller font.  #>
    param([Parameter(Mandatory = $true)][string] $What, [string] $Why)
    Write-Host ('  ? ' + $What + ' -- could not be read: ' + $Why)
    $unread.Add($What + ' :: ' + $Why)
}

function LogLines {
    <#  The demo's log as it stands. Opened with FileShare.ReadWrite because the demo holds it open;
        a plain Get-Content fails against a live Serilog file.  #>
    [OutputType([string[]])]
    param()

    if (-not $script:logFile -or -not (Test-Path $script:logFile)) { return @() }

    $stream = [System.IO.File]::Open(
        $script:logFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object System.IO.StreamReader($stream)
        $text = $reader.ReadToEnd()
    }
    finally { $stream.Dispose() }

    return @($text -split "`r?`n" | Where-Object { $_.Length -gt 0 })
}

function LogMark {
    <#  ⛔ A count, not a grep of the whole file. The log is a shared file that outlives the run, so
        "a line appeared" has to mean "since this step began" or a previous run's lines are read as
        this one's.  #>
    [OutputType([int])]
    param()

    return (LogLines).Count
}

function LogSince {
    [OutputType([string[]])]
    param([Parameter(Mandatory = $true)][int] $Mark)
    return @(LogLines | Select-Object -Skip $Mark)
}

function WaitForReady {
    <#  Waits for the view to reach Ready, which the library logs as a state transition at Information.

        ⛔ This replaced `Start-Sleep -Seconds 3`, and the three seconds were not merely arbitrary —
        they were almost exactly wrong. Measured on this machine, the demo reaches Ready about 3.0 s
        after launch, so the pass clicked the left pane at the moment the view became ready and
        sometimes landed before it: the first focus assertion failed while the second, further into
        the run, passed. A sleep tuned to a machine is a flake waiting for a slower one, and a flaky
        assertion is worse than none — an attended pass is abandoned the second time it cries wolf.  #>
    [OutputType([bool])]
    param([Parameter(Mandatory = $true)][int] $Mark, [int] $TimeoutSeconds = 30)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (@(LogSince $Mark | Where-Object { $_ -like '*State*"Ready"*' }).Count -gt 0) { return $true }
        Start-Sleep -Milliseconds 250
    }

    return $false
}

Write-Host '=== launching; the demo takes the foreground from here until DONE ==='
$report = @(& dotnet run scripts/drive-demo.cs -- launch --left $left --right $right --edit both 2>&1)
Write-Host ($report -join "`n")
$line = $report | Where-Object { $_ -match 'launched pid=(\d+)' } | Select-Object -First 1
if (-not $line) { throw 'the demo did not launch; nothing else can run' }
$demoPid = [int] ($line -replace '.*launched pid=(\d+).*', '$1')

# The log this run writes to, taken once: the demo buckets its files, so the newest at launch is the
# one this run appends to, and resolving it per step could cross a bucket boundary mid-pass.
if ($line -match 'logs=(.+?)\s*$') {
    $logDirectory = $Matches[1]
    $script:logFile = (Get-ChildItem $logDirectory -Filter 'diffview*' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1)?.FullName
}

if ($script:logFile) { Write-Host ('reading the demo log at ' + $script:logFile) }
else { Write-Host 'WARNING: no demo log found, so every log-based assertion will be unread' }

# Ready, not a stopwatch. See WaitForReady: three seconds was almost exactly the wrong number here.
$launchMark = LogMark
if (WaitForReady -Mark $launchMark) { Write-Host 'the view reached Ready; starting' }
else {
    Write-Host 'WARNING: no Ready transition was logged; starting anyway, and early steps may be racing a build'
    Start-Sleep -Seconds 3
}

try {
    # ---- the focus accent, which is a collapsed overlay in the header and must move no row ----
    Write-Host '=== focus accent ==='
    Focus 'SideBySide/LeftPane' | Out-Null
    Start-Sleep -Milliseconds 600
    Shot 'focus-left' | Out-Null

    # Both panes are asked, not just the one clicked: "the left pane has the keyboard" is also true
    # of a frame where BOTH somehow claim it, and the accent is about which one.
    AssertFocus 'the left pane holds the keyboard' 'SideBySide/LeftPane' 'SideBySide/RightPane'

    Focus 'SideBySide/RightPane' | Out-Null
    Start-Sleep -Milliseconds 600
    Shot 'focus-right' | Out-Null

    AssertFocus 'the keyboard moved to the right pane' 'SideBySide/RightPane' 'SideBySide/LeftPane'

    # ---- the pane's own context menu at real DPI, and light-dismiss ----
    Write-Host '=== pane context menu ==='
    if (Drive -Verbs @('mark', 'then', 'click', 'right', '--id', 'SideBySide/LeftPane', 'then', 'popup') -What 'right-click the left pane') {
        Shot 'pane-menu' 'popup' | Out-Null
    }

    # A popup appearing says a window opened; it does not say the menu has anything in it. The entries
    # carry ids, so reaching for one is stronger for free — `NextChange` is on every pane menu.
    $entry = Probe 'NextChange'
    if ($entry.Read) { Assert 'the pane menu has its entries' $entry.Found 'no NextChange entry is in the tree' }
    else { Unread 'the pane menu entries' 'the probe could not run' }

    Drive -Verbs @('key', 'Escape') -What 'Escape to light-dismiss the menu' | Out-Null
    Start-Sleep -Milliseconds 800
    Shot 'after-light-dismiss' | Out-Null

    # ⛔ The dismiss is the assertion, not the frame. A captured frame cannot show that a menu closed
    # — it shows the window as drawn, and a menu drawn in its own popup is simply not in it.
    $entry = Probe 'NextChange'
    if ($entry.Read) { Assert 'the menu is gone after Escape' (-not $entry.Found) 'its NextChange entry is still in the tree' }
    else { Unread 'whether the menu closed' 'the probe could not run' }

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

    $palette = Probe 'ColourBlindPalette'
    if ($palette.Read) { Assert 'the View menu is open and populated' $palette.Found 'its Colour-blind palette entry is not in the tree' }
    else { Unread 'whether the View menu opened' 'the probe could not run' }

    # Closed explicitly, whether or not it opened: a popup left over the pane is what refused the
    # first run's next click, and every step after it cascaded.
    Drive -Verbs @('key', 'Escape') -What 'Escape to close the View menu' | Out-Null
    Start-Sleep -Milliseconds 800

    # ---- navigation and the current-change border at real size ----
    Write-Host '=== F7 navigation ==='
    Focus | Out-Null
    $mark = LogMark
    Drive -Verbs @('key', 'F7') -What 'F7 once' | Out-Null
    Drive -Verbs @('key', 'F7') -What 'F7 twice' | Out-Null
    Start-Sleep -Milliseconds 800
    Shot 'after-f7-twice' | Out-Null

    # Two halves, because they fail differently: the command not firing at all, and the command firing
    # and the view going nowhere. The second is what the demo's landing line was added for — nothing in
    # the automation tree exposes the current change.
    if ($script:logFile) {
        $since = LogSince $mark
        $fired = @($since | Where-Object { $_ -like '*Command NextChange from the keyboard*' })
        $landed = @($since | Where-Object { $_ -match 'Now at change (\d+) of' })

        Assert 'F7 ran NextChange twice' ($fired.Count -eq 2) ($fired.Count.ToString() + ' NextChange lines since the step began')
        if ($landed.Count -gt 0 -and $landed[-1] -match 'Now at change (\d+) of') {
            Assert 'two presses left the second change current' ($Matches[1] -eq '2') ('the log says change ' + $Matches[1])
        }
        else { Unread 'where F7 landed' 'no landing line was written' }
    }
    else { Unread 'F7 navigation' 'there is no demo log to read' }

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

    }

    # Through Assert like every other step, so it prints its ✓ or ✗ and is counted the same way. The
    # hover verb throws when no tooltip appears within three seconds, so a false here means the pointer
    # was over no changed row — the margin's tooltip exists for a changed line and for nothing else.
    Assert 'the change-marker tooltip appeared, so the pointer was on a changed row' $markerShown `
        'no tooltip at the margin centre or at eight rows either side of it'

    # ---- the find bar, which is hidden until asked for ----
    Write-Host '=== the find bar ==='
    Focus | Out-Null

    # Absent BEFORE as well as present after. Without the first reading, a find bar that had been open
    # since some earlier step would satisfy the second and prove nothing about Ctrl+F.
    $bar = Probe 'SideBySide/FindBar'
    if ($bar.Read) { Assert 'the find bar starts closed' (-not $bar.Found) 'it is already in the tree before Ctrl+F' }
    else { Unread 'whether the find bar starts closed' 'the probe could not run' }

    Drive -Verbs @('key', 'ctrl+f') -What 'Ctrl+F to open the find bar' | Out-Null
    Start-Sleep -Seconds 1
    Shot 'find-bar' | Out-Null

    $bar = Probe 'SideBySide/FindBar'
    if ($bar.Read) { Assert 'Ctrl+F opened the find bar' $bar.Found 'it is not in the tree after Ctrl+F' }
    else { Unread 'whether Ctrl+F opened the find bar' 'the probe could not run' }

    Drive -Verbs @('key', 'Escape') -What 'Escape to close the find bar' | Out-Null
    Start-Sleep -Milliseconds 600

    $bar = Probe 'SideBySide/FindBar'
    if ($bar.Read) { Assert 'Escape closed the find bar' (-not $bar.Found) 'it is still in the tree after Escape' }
    else { Unread 'whether Escape closed the find bar' 'the probe could not run' }

    # ---- the splitter, which is the connector gutter's empty column ----
    # AGENTS.md section 6: the headers and the panes share a column layout, so a header must stay
    # exactly as wide as its pane AND start where it starts. That contract was broken for four
    # plans by two stale spacer numbers that summed to the same total, which no headless test
    # caught. A frame after a drag is where it shows.
    Write-Host '=== splitter drag ==='
    # ⛔ To the top first. The splitter is the connector column's EMPTY part, and a press on a
    # polygon selects that block instead -- so where the drag starts depends on what is scrolled
    # into view. Ctrl+Home puts the unchanged head of both files on screen, where the column is
    # empty; at the position F7 left behind, y=364 was inside a polygon and the drag moved nothing.
    Focus | Out-Null
    Drive -Verbs @('key', 'ctrl+Home') -What 'Ctrl+Home, so the drag starts on empty connector column' | Out-Null
    Start-Sleep -Milliseconds 600
    Shot 'split-before' | Out-Null

    $paneBefore = ElementRect -Path 'SideBySide/LeftPane' -DemoPid $demoPid
    if (Drive -Verbs @('drag', 'left', '539', '150', 'to', '689', '150') -What 'drag the splitter 150px right') {
        Start-Sleep -Milliseconds 800
        Shot 'split-after' | Out-Null
    }

    # ⛔ That `ok` says the press, the walk and the release all took. It does NOT say the splitter
    # moved: nothing refuses a drag that lands on the wrong thing, which is exactly how this step
    # passed while measuring block selection. The pane's own width is the evidence.
    $paneAfter = ElementRect -Path 'SideBySide/LeftPane' -DemoPid $demoPid
    if ($null -ne $paneBefore -and $null -ne $paneAfter) {
        Assert 'the splitter drag moved the panes' ($paneBefore.W -ne $paneAfter.W) `
            ('the left pane is still ' + $paneAfter.W + ' px wide, so the drag began on a connector polygon, which selects a block')
        if ($paneBefore.W -ne $paneAfter.W) {
            Write-Host ('       the left pane went from ' + $paneBefore.W + ' px to ' + $paneAfter.W + ' px')
        }
    }
    else { Unread 'whether the splitter moved' 'the left pane bounds could not be read either side of the drag' }

    # ---- the overview map's viewport box, where a press inside drags and a press outside jumps ----
    # ⛔ Its start must land INSIDE the viewport box: a press inside drags, a press outside jumps. That
    # depends on the scroll position, which is the splitter step's Ctrl+Home above — so these two steps
    # are ordered, not merely adjacent. The reading below is what turns a wrong gesture into a failure.
    Write-Host '=== overview map drag ==='
    $scrollBar = 'SideBySide/RightPane/PART_VerticalScrollBar'
    $scrolledBefore = Probe $scrollBar

    if (Drive -Verbs @('drag', 'left', '1089', '90', 'to', '1089', '420') -What 'drag the map viewport down') {
        Start-Sleep -Milliseconds 800
        Shot 'map-dragged' | Out-Null
    }

    $scrolledAfter = Probe $scrollBar
    if ($scrolledBefore.Read -and $scrolledAfter.Read -and $null -ne $scrolledBefore.Range -and $null -ne $scrolledAfter.Range) {
        Assert 'the map drag scrolled the panes' ($scrolledBefore.Range -ne $scrolledAfter.Range) `
            ('the scroll bar is still at ' + $scrolledAfter.Range + ', so the press missed the viewport box and jumped nowhere')
        if ($scrolledBefore.Range -ne $scrolledAfter.Range) {
            Write-Host ('       the scroll went from ' + $scrolledBefore.Range + ' to ' + $scrolledAfter.Range)
        }
    }
    else {
        # ⚠ PART_VerticalScrollBar is AvaloniaEdit's template part, not one of ours, and nothing pins
        # it: an Avalonia upgrade may rename it, and this is where that shows.
        Unread 'whether the map drag scrolled anything' ('no RangeValue at ' + $scrollBar)
    }

    # ---- the colour-blind palette, which the capture half cannot reach: there is no flag for it ----
    # LAST, because it repaints everything. Its entry has x:Name="ColourBlindPalette" and Avalonia
    # derives an AutomationId from a Name, so it is clickable by id inside the open popup rather
    # than by counting rows down a menu.
    Write-Host '=== colour-blind palette ==='
    if (Drive -Verbs @('mark', 'then', 'click', 'left', '96', '17', 'then', 'popup') -What 'open the View menu for the palette') {
        $mark = LogMark
        if (Drive -Verbs @('click', 'left', '--id', 'ColourBlindPalette') -What 'tick Colour-blind palette') {
            Start-Sleep -Seconds 1
            Shot 'palette-colour-blind' | Out-Null

            # The demo logs a checkable menu item with the state it left, which is the only readable
            # answer: a MenuItem advertises ExpandCollapse and no Toggle, so UI Automation cannot say
            # whether this one is ticked.
            if ($script:logFile) {
                $ticked = @(LogSince $mark | Where-Object { $_ -match 'Menu: .*(Colour|Color)-blind palette → on' })
                Assert 'the palette is now the colour-blind one' ($ticked.Count -eq 1) `
                    'no "→ on" line for the palette since the click'
            }
            else { Unread 'whether the palette was ticked' 'there is no demo log to read' }
        }
        else {
            # The click itself was refused — by the driver's own guard, which is what happened for
            # three runs before `Drawn` taught it which window a menu entry is drawn in. The refusal
            # is already a recorded failure; this says the state it was going to assert went unread,
            # so the two are not confused for one another.
            Unread 'whether the palette was ticked' 'the click on its entry was refused'
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
        Write-Host 'every step took and every assertion held'
    }
    else {
        Write-Host ('WHAT DID NOT HOLD (' + $failures.Count + '):')
        foreach ($f in $failures) { Write-Host ('  - ' + $f) }
    }

    # ⛔ Named, never counted as a pass. A run reporting twelve of twelve while some of its readings
    # could not be taken is the same lie this whole pass was rewritten to stop telling.
    if ($unread.Count -gt 0) {
        Write-Host ''
        Write-Host ('READINGS THAT COULD NOT BE TAKEN (' + $unread.Count + ') — these steps proved NOTHING:')
        foreach ($u in $unread) { Write-Host ('  ? ' + $u) }
    }

    Write-Host ''
    Write-Host 'Every frame is gated on its own step, and every step that can be asserted is. A step'
    Write-Host 'that did nothing now says so; the two above are the lists to read, not the frame names.'
    Write-Host ''
    Write-Host 'STILL NOT COVERED anywhere, and owed:'
    Write-Host '  macOS -- plan 00023 specifies that back end and deliberately does not write it,'
    Write-Host '  because its Accessibility and Screen Recording grants are interactive.'
    Write-Host ''
    Write-Host 'DONE -- the desktop is yours again.'
}
