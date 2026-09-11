namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// The pair plan 00013's tests fold. Blocks of each shape, so the boundary rule has all of its
/// cases: a modification, which leaves both sides a line; an insertion and a deletion, which each
/// leave one side padding; and a one-sided block at the very end, whose gap rides on the other
/// side's last line.
/// </summary>
internal static class FoldingFixture
{
    public static (string Left, string Right) Pair()
    {
        List<string> left = [];
        List<string> right = [];
        int same = 0;

        // Numbered from a counter, not from the builders' lengths: those diverge the moment one
        // side gets a line the other does not, and then the "same" rows are not the same at all.
        void Same(int count)
        {
            for (int i = 0; i < count; i++)
            {
                same++;
                left.Add("same " + same);
                right.Add("same " + same);
            }
        }

        Same(20);
        left.AddRange(["only on the left 1", "only on the left 2", "only on the left 3"]);
        Same(20);
        right.AddRange(["only on the right 1", "only on the right 2"]);
        Same(20);
        left.Add("changed here");
        right.Add("CHANGED HERE");
        Same(20);

        // Joined rather than terminated, and the extra lines go last: a file that ends with a
        // newline has a final empty line, both sides get one, and the two pair up — so the
        // trailing gap this fixture exists to produce would never exist.
        left.AddRange(["trailing left 1", "trailing left 2", "trailing left 3"]);

        return (string.Join('\n', left), string.Join('\n', right));
    }
}
