#!/usr/bin/env pwsh
#Requires -Version 7
# Regenerate docs/template-parts.md, the reference to every control's template parts.
# The test is the generator and has no write mode: on drift it fails, writing the fresh reference beside the
# committed one, and this moves that over and runs the test again. A failure that wrote nothing, a check's or
# a build's, moves nothing, and deleting any received copy first is what makes that true.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$project = 'tests/DiffView.Avalonia.Tests/DiffView.Avalonia.Tests.csproj'
$class = 'Bennewitz.Ninja.DiffView.Tests.TemplatePartReferenceTests'
$committed = 'docs/template-parts.md'
$received = 'docs/template-parts.received.md'

if (Test-Path -LiteralPath $received) {
    Remove-Item -LiteralPath $received
}

& dotnet test --project $project --filter-class $class
if ($LASTEXITCODE -eq 0) {
    "$committed is up to date."
    exit 0
}

if (-not (Test-Path -LiteralPath $received)) {
    [Console]::Error.WriteLine("The test failed and wrote no reference, so $committed is left as it was.")
    exit 1
}

Move-Item -LiteralPath $received -Destination $committed -Force
& dotnet test --project $project --no-build --filter-class $class
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

"$committed is rewritten from the code. Read what moved, and commit it."
exit 0
