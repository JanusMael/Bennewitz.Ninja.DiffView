using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

public sealed class DiffSideTests
{
    [Fact]
    public void Sides_are_exactly_left_and_right()
    {
        DiffSide[] sides = Enum.GetValues<DiffSide>();

        Assert.Equal([DiffSide.Left, DiffSide.Right], sides);
        Assert.NotEqual(DiffSide.Left, DiffSide.Right);
    }
}
