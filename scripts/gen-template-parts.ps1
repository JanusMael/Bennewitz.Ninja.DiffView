#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Rewrites docs/template-parts.md, the reference to every control's template parts, from the code.

.DESCRIPTION
    The test is the generator and has no write mode: on drift it fails, writing the fresh reference beside the
    committed one, and this moves that over and runs the test again. A failure that wrote nothing, a check's or a
    build's, moves nothing, and deleting any received copy first is what makes that true.

    It asks nothing before rewriting the reference: running it is the request, and git holds what it replaces.
    -WhatIf prints the steps and changes nothing; -Confirm adds nothing.

    Exits 0 when the reference is what the code says, 1 when the test failed, and 2 when it could not run, an
    invalid parameter among the reasons.

.PARAMETER Rest
    Any argument the other parameters don't take, which is an error.

.EXAMPLE
    pwsh -NoProfile -File scripts/gen-template-parts.ps1

.EXAMPLE
    pwsh -NoProfile -File scripts/gen-template-parts.ps1 -WhatIf
#>

[CmdletBinding(SupportsShouldProcess, PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments)] [string[]] $Rest
)

function Test-OutputRedirected {
    # Whether this process's output goes somewhere other than a terminal; a seam a test can redefine.
    [Console]::IsOutputRedirected
}

function Test-Colour {
    # An editor's host colours whatever the redirection; a console colours only when its output is a terminal.
    if ($Host.Name -ne 'ConsoleHost') { return $true }
    -not (Test-OutputRedirected)
}

function Write-Tag([string] $Tag, [string] $Text) {
    $line = '{0,-5} {1}' -f $Tag, $Text
    if (Test-Colour) {
        $colour = @{ 'ok' = 'Green'; '..' = 'DarkGray'; 'would' = 'Cyan'; 'FAIL' = 'Red' }[$Tag]
        Write-Host $line -ForegroundColor $colour
    } else {
        Write-Host $line
    }
}

function Invoke-TemplatePartsTest([string[]] $Arguments) {
    # The test's output goes to the host, so that it reaches the person and not this function's return value.
    & dotnet test --project tests/DiffView.Avalonia.Tests/DiffView.Avalonia.Tests.csproj @Arguments `
        --filter-class Bennewitz.Ninja.DiffView.Tests.TemplatePartReferenceTests | Out-Host
    $LASTEXITCODE
}

function Invoke-GenTemplateParts([string[]] $Rest) {
    $ErrorActionPreference = 'Stop'
    $PSNativeCommandUseErrorActionPreference = $false
    if ($null -ne $Rest -and $Rest.Count) {
        Write-Tag 'FAIL' "'$($Rest[0])' is not a parameter gen-template-parts takes; -? lists them."
        return 2
    }

    $committed = 'docs/template-parts.md'
    $received = 'docs/template-parts.received.md'
    $scripts = $PSScriptRoot ? $PSScriptRoot : (Split-Path -Parent $PSCommandPath)
    Push-Location -LiteralPath (Split-Path -Parent $scripts)
    try {
        if ($WhatIfPreference) {
            Write-Tag 'would' "delete $received, if it is there"
            Write-Tag 'would' 'run the test'
            Write-Tag 'would' "if it fails having written $received, move that over $committed and run it again"
            return 0
        }

        if (Test-Path -LiteralPath $received) {
            Remove-Item -LiteralPath $received
        }

        Write-Tag '..' 'running the test, which fails on drift'
        if ((Invoke-TemplatePartsTest @()) -eq 0) {
            Write-Tag 'ok' "$committed is up to date"
            return 0
        }

        if (-not (Test-Path -LiteralPath $received)) {
            Write-Tag 'FAIL' "the test failed and wrote no reference, so $committed is left as it was"
            return 1
        }

        Move-Item -LiteralPath $received -Destination $committed -Force
        Write-Tag '..' 'took the fresh reference, and running the test again'
        if ((Invoke-TemplatePartsTest @('--no-build')) -ne 0) {
            Write-Tag 'FAIL' "the test still fails over the fresh reference, which is in $committed"
            return 1
        }

        Write-Tag 'ok' "$committed is rewritten from the code. Read what moved, and commit it"
        return 0
    } catch {
        Write-Tag 'FAIL' $_.Exception.Message
        return 2
    } finally {
        Pop-Location
    }
}

function Exit-Script([int] $Code) {
    # A bare exit would close an editor's host that dot-sourced this file; there a failure throws instead.
    if ($Host.Name -ne 'ConsoleHost') {
        if ($Code -ne 0) { throw "gen-template-parts finished with exit code $Code" }
        return
    }
    exit $Code
}

if ($env:GEN_TEMPLATE_PARTS_NOEXEC) {
    Write-Host 'gen-template-parts: GEN_TEMPLATE_PARTS_NOEXEC is set, so nothing runs.'
} else {
    Exit-Script (Invoke-GenTemplateParts -Rest $Rest)
}
