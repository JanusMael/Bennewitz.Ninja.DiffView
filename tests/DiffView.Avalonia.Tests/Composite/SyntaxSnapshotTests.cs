using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Tests.Presenter;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 9, the rendered evidence: a C# and a JSON pair colourised under the diff
/// backgrounds in both variants, and the pixel assertion that the two layers compose — an
/// inserted row keeps its fill and the grammar's colours are on the glyphs over it.
/// </summary>
public sealed class SyntaxSnapshotTests
{
    /// <summary>The C# fixture's <c>using System;</c>: two token colours on one line.</summary>
    private const int KeywordLine = 1;

    /// <summary>The JSON fixture's first property; its line 1 is a lone brace, which is one colour.</summary>
    private const int JsonKeywordLine = 2;

    [AvaloniaTheory]
    [InlineData("Csharp", "Light")]
    [InlineData("Csharp", "Dark")]
    [InlineData("Json", "Light")]
    [InlineData("Json", "Dark")]
    [EnglishChrome]
    [LinuxBaseline]
    public async Task A_colourised_pair_renders_under_the_diff_backgrounds(string language, string variant)
    {
        TestLogSink.Instance.Clear();
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        app.RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        using MemoryStream png = new();
        try
        {
            bool json = language == "Json";
            (PaneSource left, PaneSource right) = json ? CompositeHost.JsonFixture() : CompositeHost.CsharpFixture();
            int line = json ? JsonKeywordLine : KeywordLine;
            using CompositeHost host = new(width: 900, height: 600, syntax: true);
            host.Show();
            await host.LoadAsync(left, right);
            Assert.True(
                await CompositeHost.PumpUntilAsync(
                    () => SyntaxProbe.IsColoured(host.Left, line) && SyntaxProbe.IsColoured(host.Right, line)),
                "the panes should be colourised");
            using WriteableBitmap frame = host.Capture();
            frame.Save(png, PngBitmapEncoderOptions.Default);
            Assert.Equal(json ? "json" : "csharp", host.Left.SyntaxLanguageId);
            Assert.Equal(DiffViewState.Ready, host.View.State);
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }

        TestLogSink.AssertNoWarnings(LogArea.Binding);
        png.Position = 0;
        await Verifier.Verify(png, "png").UseParameters(language, variant);
    }

    [AvaloniaFact]
    public async Task Syntax_colour_and_the_inserted_fill_compose_on_the_same_row()
    {
        (PaneSource left, PaneSource right) = CompositeHost.CsharpFixture();
        using CompositeHost host = new(width: 900, height: 600, syntax: true);
        host.Show();
        await host.LoadAsync(left, right);
        Assert.True(await CompositeHost.PumpUntilAsync(() => SyntaxProbe.IsColoured(host.Right, KeywordLine)));

        // The first inserted row of the right side: "using System.Globalization;", keywords and all.
        SideBySideDocument document = host.View.Document!;
        AlignedRow inserted = document.Rows.First(row => row.Kind == DiffLineKind.Inserted);
        int lineNumber = inserted.RightLine!.Value + 1;
        Assert.True(await CompositeHost.PumpUntilAsync(() => SyntaxProbe.IsColoured(host.Right, lineNumber)));

        using WriteableBitmap frame = host.Capture();
        TextView view = host.Right.TextArea.TextView;
        VisualLine visual = view.GetVisualLine(lineNumber) ?? throw new InvalidOperationException("the inserted line is not visible");
        Rect row = new(0, visual.VisualTop - view.ScrollOffset.Y, view.Bounds.Width, visual.Height);

        Color fill = PresenterHost.Composite(
            PresenterHost.Token("DiffView.InsertedBrush"),
            PresenterHost.Token("DiffView.PaneBackgroundBrush"));
        Assert.True(Count(host, frame, row, fill) > 0, "the inserted row should still carry its fill");

        // The grammar's colours are on the glyphs over that fill: not one foreground for the row,
        // but the several the tokens carry, each of them painted.
        HashSet<Color> colours = SyntaxProbe.Colours(host.Right, lineNumber);
        Assert.True(colours.Count > 1, "the row should carry more than one token colour");
        List<Color> painted = colours.Where(colour => Count(host, frame, row, colour) > 0).ToList();
        Assert.True(painted.Count > 1, $"more than one token colour should be painted on the inserted row; only {painted.Count} of {colours.Count} was");
    }

    /// <summary>Pixels inside a rectangle of the right pane's text view that carry <paramref name="expected"/>.</summary>
    private static int Count(CompositeHost host, WriteableBitmap frame, Rect rect, Color expected)
    {
        Point origin = host.Right.TextArea.TextView.TranslatePoint(new Point(0, 0), host.Window)
                       ?? throw new InvalidOperationException("the text view is not in the window");
        PixelRect area = PixelProbe.Inside(origin.X + rect.Left, origin.Y + rect.Top, origin.X + rect.Right, origin.Y + rect.Bottom);
        return PixelProbe.Count(frame, area, c => PresenterHost.Near(c, expected));
    }
}
