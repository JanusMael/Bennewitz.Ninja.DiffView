#!/usr/bin/env bash
# Re-measure the claim behind GridSlotTests' inert-at-pin marker: that the pinned XQ1004 never
# reports a grid placement as Skipped. Exit 0 means the claim holds at this pin; exit 1 means the
# rule can now skip, so the guard is owed a mutation rather than a marker. Run it when the pin moves.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/xq1004-skips.cs -- "$@"
