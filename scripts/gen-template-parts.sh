#!/usr/bin/env bash
# Rewrites docs/template-parts.md, the reference to every control's template parts, from the code.
#
# The test is the generator and has no write mode: on drift it fails, writing the fresh reference beside the
# committed one, and this moves that over and runs the test again. A failure that wrote nothing, a check's or
# a build's, moves nothing, and deleting any received copy first is what makes that true.
#
# It asks nothing before rewriting the reference: running it is the request, and git holds what it replaces.
#
#   --dry-run   prints the steps and changes nothing
#   --help      prints this
#
# Exits 0 when the reference is what the code says, 1 when the test failed, and 2 when it could not run, an
# invalid option among the reasons.
set -eEu -o pipefail

tag() {
    if [ -t 1 ]; then
        printf '\033[%sm%-5s %s\033[0m\n' "$1" "$2" "$3"
    else
        printf '%-5s %s\n' "$2" "$3"
    fi
}
ok() { tag 32 ok "$1"; }
step() { tag 90 .. "$1"; }
would() { tag 36 would "$1"; }
fail() { tag 31 FAIL "$1"; }

dry_run=0
for argument in "$@"; do
    case "$argument" in
        --dry-run) dry_run=1 ;;
        --help)
            sed -n '2,14s/^# \{0,1\}//p' "$0"
            exit 0
            ;;
        *)
            fail "'$argument' is not an option gen-template-parts takes; --help lists them."
            exit 2
            ;;
    esac
done

cd "$(dirname "$0")/.." || {
    fail "could not reach the repository beside $0"
    exit 2
}
if ! command -v dotnet >/dev/null 2>&1; then
    fail "dotnet is not on the PATH"
    exit 2
fi

project=tests/DiffView.Avalonia.Tests/DiffView.Avalonia.Tests.csproj
class=Bennewitz.Ninja.DiffView.Tests.TemplatePartReferenceTests
committed=docs/template-parts.md
received=docs/template-parts.received.md

if [ "$dry_run" -eq 1 ]; then
    would "delete $received, if it is there"
    would "run the test"
    would "if it fails having written $received, move that over $committed and run it again"
    exit 0
fi

rm -f "$received"
step "running the test, which fails on drift"
if dotnet test --project "$project" --filter-class "$class"; then
    ok "$committed is up to date"
    exit 0
fi

if [ ! -f "$received" ]; then
    fail "the test failed and wrote no reference, so $committed is left as it was"
    exit 1
fi

mv -f "$received" "$committed"
step "took the fresh reference, and running the test again"
if ! dotnet test --project "$project" --no-build --filter-class "$class"; then
    fail "the test still fails over the fresh reference, which is in $committed"
    exit 1
fi

ok "$committed is rewritten from the code. Read what moved, and commit it"
