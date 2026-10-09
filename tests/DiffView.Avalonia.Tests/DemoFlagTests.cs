using Bennewitz.Ninja.DiffView.Demo;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The demo's command line, where a by-hand pass starts. Open item 9's Windows run found
/// <c>--palette</c> missing: the capture half takes a window's pixels without the foreground and
/// drives nothing, so the colour-blind palette — reachable only through the View menu — was the one
/// palette that half structurally could not photograph, its whole purpose being that some readers
/// cannot use the other.
/// </summary>
/// <remarks>
/// <see cref="DebugFlags"/> is static, so every test here resets it in a <c>finally</c>; the suite
/// runs serially, and a leaked flag would otherwise reach every test after it.
/// </remarks>
public sealed class DemoFlagTests
{
    [Fact]
    public void The_palette_flag_takes_both_spellings_and_defaults_to_the_default_palette()
    {
        try
        {
            DebugFlags.Parse([]);
            Assert.False(DebugFlags.ColourBlindPalette);

            DebugFlags.ResetForTesting();
            DebugFlags.Parse(["--palette", "colour-blind"]);
            Assert.True(DebugFlags.ColourBlindPalette);

            // Half the estate types the other spelling; the demo's own menu says "Colour-blind".
            DebugFlags.ResetForTesting();
            DebugFlags.Parse(["--palette", "color-blind"]);
            Assert.True(DebugFlags.ColourBlindPalette);

            DebugFlags.ResetForTesting();
            DebugFlags.Parse(["--palette", "default"]);
            Assert.False(DebugFlags.ColourBlindPalette);
        }
        finally
        {
            DebugFlags.ResetForTesting();
        }
    }

    [Fact]
    public void A_palette_nobody_ships_is_refused_rather_than_guessed()
    {
        try
        {
            DebugFlags.Parse(["--palette", "chartreuse"]);

            // Refused, not approximated: a flag that silently did nothing would put a by-hand pass
            // in front of the default palette believing it was looking at the other one.
            Assert.False(DebugFlags.ColourBlindPalette);
        }
        finally
        {
            DebugFlags.ResetForTesting();
        }
    }

    [Fact]
    public void The_summary_names_the_palette_so_a_pass_can_confirm_the_flag_took()
    {
        try
        {
            // The rule EditSummary records: a flag the summary does not name is a flag a by-hand
            // pass cannot confirm took effect, which is how the --edit gap was found.
            Assert.Equal("default", DebugFlags.PaletteSummary);

            DebugFlags.Parse(["--palette", "colour-blind"]);
            Assert.Equal("colour-blind", DebugFlags.PaletteSummary);
        }
        finally
        {
            DebugFlags.ResetForTesting();
        }
    }
}
