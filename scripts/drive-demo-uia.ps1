#Requires -Version 7
<#
.SYNOPSIS
Finds one of the demo's parts by a path of AutomationIds and reports where it is.

.DESCRIPTION
The reader half of the Windows back end of scripts/drive-demo.cs, which is plan 00023 phase 4. It is
a script of its own, and not part of that file-based app, because UI Automation's managed client lives
in the Windows Desktop framework: referencing it would make a portable driver a Windows-only one.

A part is addressed by its AutomationId, never by its name — the names are translated into eight
locales, so a search for "Left pane" finds nothing on a German machine. Each step of the path is an id
unique within the step before it, which is the scope DECISIONS.md's *Plan 00023's Windows back end
finds DiffView's parts by `AutomationId`* lays down, and fixtures/automation-ids.txt is the list:

    -Path SideBySide                       a view, within the demo's window
    -Path SideBySide/LeftPane              a part, within the view
    -Path SideBySide/LeftPane/LineNumbers  a margin, within the pane

It prints one tab-separated line — path, control type, x, y, width, height, window handle, whether the
part holds the keyboard, and a RangeValue reading or `-` — the pixels absolute and physical and the
number invariant, so nothing the caller reads depends on a locale.

Exit 0 found it, 3 the part is ABSENT, 4 could not look, 5 the demo has no window at all. ⛔ 3 and 5
were one code until plan 00033: presence is an assertion for a harness — a part switched off is
genuinely gone from the tree — so "this part is not there" must not read the same as "the demo died".

.NOTES
Scoped to the demo's process and then down the path, never across the desktop: XamlQuality's
docs/ai-drivable-ui.md measures an unscoped descendant search at 94 s on a quiet desktop and 406 s on
a busy one, and records one that invoked another application's button. Its rule about never naming a
variable $pid is why the process is -DemoProcessId here: $pid is this script's own process.

.EXAMPLE
./scripts/drive-demo-uia.ps1 -DemoProcessId 4242 -Path SideBySide/LeftPane
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int] $DemoProcessId,
    [Parameter(Mandatory = $true)][string] $Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Straight to the error stream, because Write-Error decorates a message with colour and a frame of the
# script's own source, and this one is read by the driver and shown to a person as the reason a verb
# could not run.
function Fail([string] $Message) { [Console]::Error.WriteLine($Message) }

try {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
}
catch {
    Fail ('UI Automation is not available to this PowerShell: ' + $_.Exception.Message)
    exit 4
}

# ⛔ @() at the ASSIGNMENT. A pipeline that yields one element yields it as a SCALAR, and under
# Set-StrictMode the .Count below then fails with "The property 'Count' cannot be found on this
# object" — so a one-step path like `ColourBlindPalette` crashed the reader while every two- and
# three-step path worked. Every id this driver had ever been given was a path until a menu entry,
# whose id stands alone, which is why it survived PR #16 and a by-hand pass before it fired.
$steps = @($Path.Split('/') | Where-Object { $_.Length -gt 0 })
if ($steps.Count -eq 0) {
    Fail 'the path names no ids'
    exit 4
}

# The demo's windows are children of the desktop root. A context menu is a top-level window of its own,
# so it is found here too rather than under the main window.
$root = [System.Windows.Automation.AutomationElement]::RootElement
$byProcess = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $DemoProcessId)
$windows = @($root.FindAll([System.Windows.Automation.TreeScope]::Children, $byProcess))

if ($windows.Count -eq 0) {
    # 5, not 3: the demo being gone is not the same answer as a part being absent, and a harness that
    # asserts absence would read the two as one.
    Fail ('no window belongs to process ' + $DemoProcessId + ': is the demo still running?')
    exit 5
}

# Each step is searched for beneath every window of the process, because the first step may name a view
# in the main window or an entry in a menu that is its own window.
$found = $null
$foundIn = $null
foreach ($window in $windows) {
    $scope = $window
    $reached = $true
    foreach ($step in $steps) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $step)
        $next = $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -eq $next) { $reached = $false ; break }
        $scope = $next
    }

    if ($reached) { $found = $scope ; $foundIn = $window ; break }
}

if ($null -eq $found) {
    Fail ('no part at ' + $Path + ' in process ' + $DemoProcessId +
        '. A part that is not on screen is not in the tree: the map, the headers, the strip, the ' +
        "banner's action and the find bar are hidden when they are off, so this may be a part " +
        'switched off rather than a missing one.')
    exit 3
}

# Re-read rather than trusting anything cached: a stale element answers with its last rectangle.
$current = $found.Current
$rectangle = $current.BoundingRectangle
if ($rectangle.IsEmpty) {
    Fail ('the part at ' + $Path + ' reports no rectangle, so it is not on screen')
    exit 3
}

# The top-level window the part was found in, so the caller can check that a click at these
# coordinates would land on it rather than on whatever is lying over the demo. A part's is the demo's
# window; a menu entry's is the menu's own, a menu being a window of its own here.
# Whether the part holds the keyboard. The pane's own peer answers this for its text area rather than
# for itself, because AvaloniaEdit's TextEditor is not focusable and the stock peer would read the
# wrong thing (AGENTS.md §1) — so this is the answer a harness wants, not a near miss.
$focus = $current.HasKeyboardFocus ? 'focused' : 'unfocused'

# A RangeValue reading where the element has one, and `-` where it has none. This is the only way the
# scroll offset is readable: our own peers advertise no pattern by design, but the stock scroll bars
# inside AvaloniaEdit's template advertise RangeValue, which plan 00033 measured.
#
# ⛔ TryGetCurrentPattern, never GetCurrentPattern, which THROWS for an element that does not support
# the pattern — and most do not.
# ⛔ InvariantCulture explicitly: a double renders as "0,5" on a German machine, and the parser on the
# other side of this report expects a dot. The report is machine-read, so no part of it is localised.
$range = '-'
$rangePattern = $null
if ($found.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref] $rangePattern)) {
    $range = ([double] $rangePattern.Current.Value).ToString([System.Globalization.CultureInfo]::InvariantCulture)
}

$fields = @(
    $Path
    $current.ControlType.ProgrammaticName
    [int] [Math]::Round([double] $rectangle.X)
    [int] [Math]::Round([double] $rectangle.Y)
    [int] [Math]::Round([double] $rectangle.Width)
    [int] [Math]::Round([double] $rectangle.Height)
    [int64] $foundIn.Current.NativeWindowHandle
    $focus
    $range
)

Write-Output ($fields -join "`t")
exit 0
