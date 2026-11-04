#!/usr/bin/env bash
# Regenerate docs/template-parts.md, the reference to every control's template parts.
# The test is the generator and has no write mode: on drift it fails, writing the fresh reference beside the
# committed one, and this moves that over and runs the test again. A failure that wrote nothing, a check's or
# a build's, moves nothing, and deleting any received copy first is what makes that true.
set -eu
cd "$(dirname "$0")/.."

project=tests/DiffView.Avalonia.Tests/DiffView.Avalonia.Tests.csproj
class=Bennewitz.Ninja.DiffView.Tests.TemplatePartReferenceTests
committed=docs/template-parts.md
received=docs/template-parts.received.md

rm -f "$received"
if dotnet test --project "$project" --filter-class "$class"; then
    echo "$committed is up to date."
    exit 0
fi

if [ ! -f "$received" ]; then
    echo "The test failed and wrote no reference, so $committed is left as it was." >&2
    exit 1
fi

mv -f "$received" "$committed"
dotnet test --project "$project" --no-build --filter-class "$class"
echo "$committed is rewritten from the code. Read what moved, and commit it."
