#!/usr/bin/env bash
# Runs the DiffView demo on a pair of files, for looking at the control by hand.
#
#   scripts/run-demo.sh                          # a pair with plenty of changes
#   scripts/run-demo.sh LEFT RIGHT               # your own pair
#   scripts/run-demo.sh --unified                # the unified view instead
#   scripts/run-demo.sh --viewer                 # the read-only viewer instead
#   scripts/run-demo.sh --variant Dark LEFT RIGHT
#
# Anything this script does not recognise is passed straight to the demo, whose own flags are
# --left, --right, --theme, --variant, --unified, --viewer, --edit, --culture and --log-level.
set -eu

here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo=$(CDPATH= cd -- "$here/.." && pwd)

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

# shellcheck disable=SC2086
exec dotnet run --project "$repo/src/DiffView.Demo" -- --left "$left" --right "$right" $extra
