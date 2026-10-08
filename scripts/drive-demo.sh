#!/usr/bin/env bash
# Drive the running demo for a by-hand pass: launch it detached, find its window and the popups it
# opens, click and type into it, capture pixels. Verbs chain with `then`:
#   scripts/drive-demo.sh launch then mark then click left 88 16 then popup then capture popup view.png
# The X11 and Windows back ends exist; macOS is specified in plan 00023 and not written.
# AGENTS.md §9 is the prose these verbs replace.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/drive-demo.cs -- "$@"
