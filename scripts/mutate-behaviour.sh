#!/usr/bin/env bash
# Prove a behaviour test able to fail: break what it guards, check it goes red, and check that a
# change which should not matter leaves it green. scripts/mutate-gates does this for the
# static-analysis gates; this is its sibling for the tests that drive the control.
#   scripts/mutate-behaviour.sh --list
#   scripts/mutate-behaviour.sh --only refold
# It refuses to start on a dirty tree, because it reverts with `git checkout`.
# Thin wrapper: the work is done by the portable .NET 10 file-based app beside this script.
set -eu
cd "$(dirname "$0")/.."
exec dotnet run scripts/mutate-behaviour.cs -- "$@"
