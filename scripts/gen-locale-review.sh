#!/usr/bin/env bash
# Regenerate docs/locale-review/<culture>.md, the documents a native speaker reviews a locale from.
# Pass a culture name to do just that one; pass --check to report staleness instead of rewriting
# (exit 1 when one differs).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/gen-locale-review.cs -- "$@"
