#!/usr/bin/env bash
# Pack the theme-audit dotnet tool (Bennewitz.Ninja.ThemeAudit) into the local NuGet feed (../nuget-local).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/pack-theme-audit.cs -- "$@"
