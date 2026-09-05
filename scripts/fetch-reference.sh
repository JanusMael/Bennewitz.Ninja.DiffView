#!/usr/bin/env bash
# Fetch the read-only upstream checkouts listed in reference/sources.json.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/fetch-reference.cs -- "$@"
