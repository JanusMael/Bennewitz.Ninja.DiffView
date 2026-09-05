#!/usr/bin/env bash
# Pack ClaudeForge's LayeredEditors.Avalonia.Diagnostics into the local NuGet feed (../nuget-local).
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/pack-diagnostics.cs -- "$@"
