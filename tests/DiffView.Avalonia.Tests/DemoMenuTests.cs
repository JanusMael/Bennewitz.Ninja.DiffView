using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Bennewitz.Ninja.DiffView.Demo;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The demo's menus are what <c>scripts/drive-demo-interactive.ps1</c> drives, and it finds every entry by
/// its <c>AutomationId</c>: each declares one of its own, unique within the menu that holds it.
/// </summary>
/// <remarks>
/// ⛔ Read off the attached property, never off a peer. Avalonia derives an id from a control's
/// <c>Name</c> when none is set, and the demo's named entries declare their <c>x:Name</c> as their id, so a
/// search of the control view would find every one of them with its explicit id deleted — the gap the
/// pass was in when it addressed the palette's entry by an id the framework had derived.
/// </remarks>
public sealed class DemoMenuTests
{
    [AvaloniaFact]
    public void Every_entry_of_the_demos_menus_declares_an_id_of_its_own_unique_within_its_menu()
    {
        TestLogSink.Instance.Clear();
        MainWindow window = new() { Width = 800, Height = 500 };
        window.Diff.Builder = CompositeHost.ZeroTimeBuilder;
        window.Unified.Builder = CompositeHost.ZeroTimeBuilder;
        window.Viewer.Builder = CompositeHost.ZeroTimeBuilder;

        // ⛔ The hand-advanced clock, set before the window shows. The demo loads its sources on Opened and
        // nothing here waits for the build, which lands after Close() and re-creates the view's status on the
        // view's clock; on the real one it arms an auto-clear that nothing disposes, and one that comes due on
        // the pool thread the headless loop has just handed back aborts the test host.
        FakeTimeProvider time = new();
        window.Diff.TimeProvider = time;
        window.Unified.TimeProvider = time;
        window.Viewer.TimeProvider = time;
        window.Show();
        try
        {
            List<Entry> entries = [.. EntriesUnder(window.MainMenu, "the menu bar", window.MainMenu.Items)];

            // ⛔ A walk that reaches nothing passes both checks below. The palette's entry sits in a submenu
            // of View, so finding it says the walk went down into the submenus as well.
            Assert.True(
                entries.Any(e => ReferenceEquals(e.Item, window.ColourBlindPalette)),
                $"The walk found {entries.Count} entries and not the colour-blind palette's, which sits in a submenu "
                + "of View, so it did not go down into the submenus and its silence about them says nothing.");

            List<string> missing =
            [
                .. entries
                    .Where(e => string.IsNullOrEmpty(AutomationProperties.GetAutomationId(e.Item)))
                    .Select(e => $"{e.Menu} ▸ {Unmarked(e.Item.Header)}"),
            ];
            Assert.True(
                missing.Count == 0,
                "These entries of the demo's menus declare no AutomationId, so a harness finds each only by an id "
                + "Avalonia derives from its Name, which a rename moves, or by its text:" + Environment.NewLine
                + string.Join(Environment.NewLine, missing.Select(m => "  " + m)));

            List<string> twice =
            [
                .. entries
                    .Where(e => !string.IsNullOrEmpty(AutomationProperties.GetAutomationId(e.Item)))
                    .GroupBy(e => (e.Owner, Id: AutomationProperties.GetAutomationId(e.Item)!))
                    .Where(g => g.Count() > 1)
                    .Select(g => $"{g.First().Menu}: {g.Key.Id} ×{g.Count()}"),
            ];
            Assert.True(
                twice.Count == 0,
                "A harness finds an entry with one search within its menu, and these ids are there more than once:"
                + Environment.NewLine + string.Join(Environment.NewLine, twice.Select(t => "  " + t)));
        }
        finally
        {
            window.Close();
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
    }

    /// <summary>An entry of a menu, the control that holds it, and that menu's path as a reader names it.</summary>
    private sealed record Entry(ItemsControl Owner, string Menu, MenuItem Item);

    /// <summary>Every entry under <paramref name="owner"/>, its submenus' entries included.</summary>
    private static IEnumerable<Entry> EntriesUnder(ItemsControl owner, string menu, IEnumerable<object?> items)
    {
        foreach (MenuItem item in items.OfType<MenuItem>())
        {
            yield return new Entry(owner, menu, item);
            foreach (Entry below in EntriesUnder(item, $"{menu} ▸ {Unmarked(item.Header)}", item.Items))
            {
                yield return below;
            }
        }
    }

    /// <summary>A header as it reads, without the underscore that marks its access key.</summary>
    private static string Unmarked(object? header) =>
        header?.ToString()?.Replace("_", string.Empty, StringComparison.Ordinal) ?? string.Empty;
}
