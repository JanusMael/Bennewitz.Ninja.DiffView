#!/usr/bin/env bash
# Runs the DiffView demo on a pair of files, for looking at the control by hand.
#
#   scripts/run-demo.sh                          # a pair with plenty of changes
#   scripts/run-demo.sh LEFT RIGHT               # your own pair
#   scripts/run-demo.sh --unified                # the unified view instead
#   scripts/run-demo.sh --viewer                 # the read-only viewer instead
#   scripts/run-demo.sh --variant Dark LEFT RIGHT
#   scripts/run-demo.sh --detach [LEFT RIGHT] [flags]   # returns once the window is up
#
# Anything this script does not recognise is passed straight to the demo, whose own flags are
# --left, --right, --theme, --variant, --unified, --viewer, --edit, --culture and --log-level.
set -eu

here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo=$(CDPATH= cd -- "$here/.." && pwd)

# --detach, given first, starts the demo in a session of its own with its output in a log file, and
# returns once its window is up, printing the pid and the log: an agent's turn boundary reaps a
# background child otherwise (AGENTS.md §9). scripts/drive-demo.cs's `launch` does the work, so there
# is one launcher and not two that drift apart.
detach=0
if [ "${1:-}" = "--detach" ]; then
  detach=1
  shift
fi

# A pair big enough that the overview map has something to show: one file against a smaller
# relative of it, so the map carries deletions, insertions and modifications at once.
left="$repo/src/DiffView.Avalonia/SideBySideDiffView.cs"
right="$repo/src/DiffView.Avalonia/InlineDiffView.cs"

extra=""
if [ "$#" -ge 2 ] && [ "${1#-}" = "$1" ] && [ "${2#-}" = "$2" ]; then
  left=$1
  right=$2
  shift 2
fi

if [ "$#" -gt 0 ]; then
  extra="$*"
fi

echo "left  $left"
echo "right $right"
echo
echo "Things to try:"
echo "  View → Control               the editor, the unified view or the read-only viewer"
echo "  View → Show overview map     turns the two-lane map beside the panes on and off"
echo "  drag the box on the map      scrolls continuously; a click anywhere else jumps"
echo "  scroll wheel over the map    scrolls the panes"
echo "  View → Edit left/right pane  then the copy arrows appear in the number margins;"
echo "                               select some lines for the selection arrow"
echo "  F7 / Shift+F7                next and previous change;  Ctrl+F  find"
echo

if [ "$detach" = 1 ]; then
  cd "$repo"
  # shellcheck disable=SC2086
  exec dotnet run scripts/drive-demo.cs -- launch --left "$left" --right "$right" $extra
fi

# shellcheck disable=SC2086
exec dotnet run --project "$repo/src/DiffView.Demo" -- --left "$left" --right "$right" $extra
