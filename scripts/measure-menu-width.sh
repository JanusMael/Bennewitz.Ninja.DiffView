#!/usr/bin/env bash
# How wide each shipped locale's menu entries are against English, in display columns — a CJK glyph
# takes two — so a by-hand pass in a locale knows what to look at first. Pass --columns <text> for
# one text's width.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/measure-menu-width.cs -- "$@"
