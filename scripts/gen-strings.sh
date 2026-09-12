#!/usr/bin/env bash
# Regenerate src/DiffView.Avalonia/Localization/Strings.resx from DiffViewStrings.EnglishDefaults.
# Pass --check to report staleness instead of rewriting (exit 1 when it differs).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/gen-strings.cs -- "$@"
