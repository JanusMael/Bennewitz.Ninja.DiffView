using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Presenter;

/// <summary>
/// Plan 00001 §Phase 4, the pane presenter: structure, priming, metadata, the gutters, the
/// read-only contract, the search panel, and every fault boundary. Pixel-level assertions on
/// bands, selection and caret are in <see cref="PresenterPixelTests"/>.
/// </summary>
public sealed class DiffPanePresenterTests
{
    private const double Tolerance = PresenterHost.Tolerance;

    [AvaloniaTheory]
    [MemberData(nameof(ThemeTargets.All), MemberType = typeof(ThemeTargets))]
    public void Renders_under_every_theme_target_with_no_binding_or_resource_warnings(string theme, string variant)
    {
        using ThemeSwap swap = ThemeSwap.To(theme, variant);
        TestLogSink.Instance.Clear();

        using PresenterHost host = PresenterHost.Small();
        host.Show();
        using WriteableBitmap frame = host.Capture();

        // The pane painted its own background token, not the host page: the template applied
        // and the token resolved under this target. Line 1 is unchanged on both sides.
        Color paneBackground = PresenterHost.Token("DiffView.PaneBackgroundBrush");
        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            Point origin = host.ToWindow(pane, new Point(0, 0));
            int x = (int)(origin.X + pane.TextArea.TextView.Bounds.Width - 8);
            Color sampled = PixelProbe.At(frame, x, (int)origin.Y + 3);
            Assert.True(PresenterHost.Near(sampled, paneBackground), $"{pane.Side} pane under {theme} {variant}: expected {paneBackground}, got {sampled}");
            Assert.Empty(pane.Faults);
        }

        host.Dispose();
        TestLogSink.AssertNoWarnings();
    }

    [AvaloniaFact]
    public void The_document_text_equals_the_source_text_and_the_line_counts_agree()
    {
        (string left, string right) = PresenterHost.SmallFixture();
        using PresenterHost host = new(left, right);
        host.Show();

        // No padding in the document: the text is the source, character for character.
        Assert.Equal(left, host.Left.Document.Text);
        Assert.Equal(right, host.Right.Document.Text);
        Assert.Equal(host.Document.Left.Lines.Count, host.Left.Document.LineCount);
        Assert.Equal(host.Document.Right.Lines.Count, host.Right.Document.LineCount);
        Assert.Equal(host.Document.Left.Info.LineCount, host.Left.Document.LineCount);
    }

    [AvaloniaFact]
    public void The_model_and_the_editor_count_the_same_lines_on_mixed_line_endings()
    {
        const string left = "alpha\r\nbeta\rgamma\ndelta\r\n";
        const string right = "alpha\r\nbeta\rgamma changed\ndelta\r\n";
        using PresenterHost host = new(left, right);
        host.Show();

        Assert.True(host.Result.Has(DiffWarningCode.MixedLineEndings));
        Assert.Equal(5, host.Left.Document.LineCount);
        Assert.Equal(host.Document.Left.Lines.Count, host.Left.Document.LineCount);
        Assert.Equal(host.Document.Right.Lines.Count, host.Right.Document.LineCount);
        Assert.Equal(DiffLineKind.Modified, host.Left.Metadata.KindOf(3));
    }

    [AvaloniaFact]
    public void After_a_load_both_extents_are_equal_before_scrolling_and_the_renderer_sees_each_lines_kind()
    {
        using PresenterHost host = PresenterHost.Small(height: 200);
        host.Show();
        TextView leftView = host.Left.TextArea.TextView;
        TextView rightView = host.Right.TextArea.TextView;
        double lineHeight = leftView.DefaultLineHeight;
        int rows = host.Document.Rows.Count;
        Assert.Equal(lineHeight, rightView.DefaultLineHeight, Tolerance);
        Assert.NotEqual(host.Left.Document.LineCount, rows);

        // Nothing has scrolled, and both height trees already know every padded line.
        Assert.Equal(0, host.Left.VerticalOffset, Tolerance);
        Assert.Equal(0, host.Right.VerticalOffset, Tolerance);
        Assert.Equal(rows * lineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, rightView.DocumentHeight, Tolerance);
        Assert.Equal(rows * lineHeight, host.Left.ExtentHeight, Tolerance);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);
        Assert.True(host.Left.PrimedLineCount > 0, "the left pane should have primed its padded lines");
        host.AssertRowsAligned();

        // The background renderer and the marker margin received each visible line's own kind and padding.
        using WriteableBitmap frame = host.Capture();
        foreach (DiffPanePresenter pane in new[] { host.Left, host.Right })
        {
            DiffPane model = host.Document.Pane(pane.Side);
            IReadOnlyList<DrawnLine> drawn = pane.BackgroundRenderer.LastDrawn;
            Assert.NotEmpty(drawn);
            Assert.Equal(pane.TextArea.TextView.VisualLines.Select(l => l.FirstDocumentLine.LineNumber), drawn.Select(d => d.LineNumber));
            foreach (DrawnLine line in drawn)
            {
                Assert.Equal(model.Lines[line.LineNumber - 1].Kind, line.Kind);
                Assert.Equal(Padding.Before(host.Document, pane.Side, line.LineNumber - 1), line.Padding.Above);
            }

            Assert.Equal(drawn.Select(d => (d.LineNumber, d.Kind)), pane.ChangeMarkerMargin.LastRendered);
        }

        IEnumerable<DrawnLine> all = host.Left.BackgroundRenderer.LastDrawn.Concat(host.Right.BackgroundRenderer.LastDrawn);
        Assert.Contains(all, d => d.Kind != DiffLineKind.Unchanged);
        Assert.Contains(all, d => d.Padding.Above > 0);

        // A font change rebases the plain lines only; the presenter re-primes on its next layout.
        host.Left.FontSize = 18;
        host.Right.FontSize = 18;
        PresenterHost.Layout();
        double newLineHeight = leftView.DefaultLineHeight;
        Assert.NotEqual(lineHeight, newLineHeight);
        Assert.Equal(rows * newLineHeight, leftView.DocumentHeight, Tolerance);
        Assert.Equal(rows * newLineHeight, rightView.DocumentHeight, Tolerance);
        Assert.Equal(host.Left.ExtentHeight, host.Right.ExtentHeight, Tolerance);
        host.AssertRowsAligned();
    }

    [AvaloniaFact]
    public void Assigning_a_new_model_reprimes_so_lines_that_lost_their_padding_return_to_one_row()
    {
        (string left, string right) = PresenterHost.SmallFixture();
        using PresenterHost host = new(left, right);
        host.Show();
        TextView view = host.Left.TextArea.TextView;
        double lineHeight = view.DefaultLineHeight;
        int rows = host.Document.Rows.Count;
        Assert.Equal(rows * lineHeight, view.DocumentHeight, Tolerance);

        // Against itself the left side has no padding: every previously padded line must return to one row.
        SideBySideDocument identical = DiffDocumentBuilder.Build(left, left).Document;
        host.Left.DiffDocument = identical;
        PresenterHost.Layout();
        Assert.Equal(identical.Version, host.Left.MetadataVersion);
        Assert.Equal(host.Left.Document.LineCount * lineHeight, view.DocumentHeight, Tolerance);
        Assert.Empty(host.Left.Faults);

        // And back: the padding returns and the extents agree again.
        host.Left.DiffDocument = host.Document;
        PresenterHost.Layout();
        Assert.Equal(host.Document.Version, host.Left.MetadataVersion);
        Assert.Equal(rows * lineHeight, view.DocumentHeight, Tolerance);
        Assert.Equal(host.Right.ExtentHeight, host.Left.ExtentHeight, Tolerance);
        host.AssertRowsAligned();
    }

    [AvaloniaFact]
    public void The_line_number_margin_shows_the_documents_own_numbers_and_nothing_over_padding_space()
    {
        using PresenterHost host = PresenterHost.Small();
        host.Show();
        DiffPanePresenter pane = host.Left;
        TextView view = pane.TextArea.TextView;
        int paddedLine = FirstPaddedLine(host, DiffSide.Left);
        PresenterHost.ScrollTo(pane, PresenterHost.TopOfLine(pane, paddedLine));
        using WriteableBitmap frame = host.Capture();

        // The numbers are the document's own, in visual-line order, each at its line's text top.
        IReadOnlyList<(int LineNumber, double Y)> rendered = pane.LineNumberMargin.LastRendered;
        Assert.Equal(view.VisualLines.Select(l => l.FirstDocumentLine.LineNumber), rendered.Select(r => r.LineNumber));
        foreach (VisualLine line in view.VisualLines)
        {
            double expectedY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
            Assert.Contains(rendered, r => r.LineNumber == line.FirstDocumentLine.LineNumber && Math.Abs(r.Y - expectedY) < Tolerance);
        }

        // Pixels: the padding rows above the padded line carry only the gutter background; its text row carries the number.
        DiffLineNumberMargin margin = pane.LineNumberMargin;
        Point origin = host.ToWindow(margin, new Point(0, 0));
        double lineHeight = view.DefaultLineHeight;
        int above = Padding.Before(host.Document, DiffSide.Left, paddedLine - 1);
        double top = PresenterHost.TopOfLine(pane, paddedLine) - pane.VerticalOffset;
        Assert.Equal(0, top, Tolerance);
        Color gutter = PresenterHost.Token("DiffView.GutterBackgroundBrush");
        PixelRect paddingRows = PixelProbe.Inside(origin.X, origin.Y + top, origin.X + margin.Bounds.Width, origin.Y + top + above * lineHeight);
        PixelRect textRow = PixelProbe.Inside(origin.X, origin.Y + top + above * lineHeight, origin.X + margin.Bounds.Width, origin.Y + top + (above + 1) * lineHeight);
        Assert.True(paddingRows.Height > 0 && textRow.Height > 0);
        Assert.Equal(0, PixelProbe.Count(frame, paddingRows, c => !PresenterHost.Near(c, gutter)));
        Assert.True(PixelProbe.Count(frame, textRow, c => !PresenterHost.Near(c, gutter)) > 0, "the number should be drawn in the line's text row");
    }

    [AvaloniaFact]
    public void No_search_panel_is_installed()
    {
        using PresenterHost host = PresenterHost.Small();
        host.Show();
        DiffPanePresenter pane = host.Left;

        Assert.DoesNotContain(pane.TextArea.DefaultInputHandler.NestedInputHandlers, h => h.GetType().Name == "SearchInputHandler");

        pane.TextArea.Focus();
        PresenterHost.Layout();
        host.Window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);
        host.Window.KeyRelease(Key.F, RawInputModifiers.Control, PhysicalKey.F, null);
        PresenterHost.Layout();
        Assert.False(pane.SearchPanel.IsOpened, "Ctrl+F must not open AvaloniaEdit's search panel");
    }

    [AvaloniaFact]
    public void IsReadOnly_is_honoured_and_defaults_to_true()
    {
        using PresenterHost host = PresenterHost.Small();
        host.Show();
        DiffPanePresenter pane = host.Left;
        string original = pane.Document.Text;
        Assert.True(pane.IsReadOnly);
        Assert.True(pane.TextArea.IsReadOnly);

        pane.TextArea.Focus();
        pane.TextArea.Caret.Offset = 0;
        PresenterHost.Layout();
        host.Window.KeyTextInput("x");
        PresenterHost.Layout();
        Assert.Equal(original, pane.Document.Text);

        pane.IsReadOnly = false;
        PresenterHost.Layout();
        Assert.False(pane.TextArea.IsReadOnly);
        host.Window.KeyTextInput("x");
        PresenterHost.Layout();
        Assert.Equal("x" + original, pane.Document.Text);
    }

    [AvaloniaFact]
    public void Metadata_for_a_different_document_renders_unknown_lines_as_unchanged_and_never_throws()
    {
        (string left, string right) = PresenterHost.SmallFixture();
        using PresenterHost host = new(left, right, height: 640);
        host.Show();
        int knownLines = host.Document.Left.Lines.Count;
        Assert.Equal(host.Document.Version, host.Left.MetadataVersion);

        // A longer document than the model knows: known lines keep their kind, the rest are unchanged and unpadded.
        host.Left.Document = new TextDocument(left + "\nextra line one\nextra line two\nextra line three");
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Empty(host.Left.Faults);
        IReadOnlyList<DrawnLine> drawn = host.Left.BackgroundRenderer.LastDrawn;
        Assert.Contains(drawn, d => d.LineNumber > knownLines);
        foreach (DrawnLine line in drawn)
        {
            if (line.LineNumber <= knownLines)
            {
                Assert.Equal(host.Document.Left.Lines[line.LineNumber - 1].Kind, line.Kind);
            }
            else
            {
                Assert.Equal(DiffLineKind.Unchanged, line.Kind);
                Assert.True(line.Padding.IsEmpty);
            }
        }

        // A shorter document than the model knows renders every line it has, and nothing throws.
        host.Left.Document = new TextDocument("only\ntwo lines");
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Empty(host.Left.Faults);
        Assert.Equal([1, 2], host.Left.BackgroundRenderer.LastDrawn.Select(d => d.LineNumber));
        Assert.Equal(2, host.Left.LineNumberMargin.LastRendered.Count);
    }

    [AvaloniaFact]
    public void A_throwing_renderer_raises_RenderFault_once_disables_itself_and_the_text_still_renders()
    {
        using PresenterHost host = PresenterHost.Small();
        host.Show();
        DiffPanePresenter pane = host.Left;
        List<RenderFaultEventArgs> faults = [];
        pane.RenderFault += (_, e) => faults.Add(e);
        ThrowingRenderer renderer = new(pane);
        pane.TextArea.TextView.BackgroundRenderers.Add(renderer);

        using WriteableBitmap first = host.Capture();
        pane.TextArea.TextView.Redraw();
        using WriteableBitmap second = host.Capture();

        Assert.Equal(1, renderer.Draws);
        Assert.True(renderer.IsDisabled);
        RenderFaultEventArgs fault = Assert.Single(faults);
        Assert.Equal("ThrowingRenderer", fault.Source);
        Assert.IsType<InvalidOperationException>(fault.Exception);
        Assert.Contains("ThrowingRenderer", fault.Message, StringComparison.Ordinal);
        Assert.True(pane.IsDegraded);
        Assert.Equal(faults, pane.Faults);

        AssertTextRendered(host, pane, second);
    }

    [AvaloniaFact]
    public void A_throwing_generator_raises_RenderFault_once_and_the_lines_render_without_padding()
    {
        using PresenterHost host = PresenterHost.Small();
        host.Show();
        DiffPanePresenter pane = host.Left;
        TextView view = pane.TextArea.TextView;
        double lineHeight = view.DefaultLineHeight;
        Assert.Equal(host.Document.Rows.Count * lineHeight, view.DocumentHeight, Tolerance);
        List<RenderFaultEventArgs> faults = [];
        pane.RenderFault += (_, e) => faults.Add(e);

        pane.PaddingSourceForTesting = _ => throw new InvalidOperationException("deliberate padding fault");
        view.Redraw();
        pane.RequestPrime();
        using WriteableBitmap first = host.Capture();
        view.Redraw();
        using WriteableBitmap second = host.Capture();

        RenderFaultEventArgs fault = Assert.Single(faults);
        Assert.Equal(nameof(PaddingElementGenerator), fault.Source);
        Assert.True(pane.PaddingGenerator.IsDisabled);
        Assert.True(pane.IsDegraded);

        // Misaligned but alive: every line is one row tall and the text is still drawn.
        Assert.All(pane.BackgroundRenderer.LastDrawn, d => Assert.True(d.Padding.IsEmpty));
        Assert.Equal(pane.Document.LineCount * lineHeight, view.DocumentHeight, Tolerance);
        AssertTextRendered(host, pane, second);

        // A new model gives the generator another chance.
        pane.PaddingSourceForTesting = null;
        pane.DiffDocument = DiffDocumentBuilder.Build(host.Document.Left.Lines.Count.ToString(), "x").Document;
        pane.DiffDocument = host.Document;
        PresenterHost.Layout();
        Assert.False(pane.PaddingGenerator.IsDisabled);
        Assert.Empty(pane.Faults);
        Assert.Equal(host.Document.Rows.Count * lineHeight, view.DocumentHeight, Tolerance);
    }

    [AvaloniaFact]
    public void Margins_carry_automation_names_through_the_string_resolver()
    {
        // A partial resolver: one key answered, 142 returning null. The culture is pinned because
        // the second assertion is about what a *null* falls through to, and leaving that to the
        // machine's UI culture is how this test would read English here and a bundled translation
        // on someone else's desk.
        using (DiffViewStrings.Override(new DiffViewLocalization
        {
            Culture = CultureInfo.InvariantCulture,
            Resolver = (key, _) => key == DiffViewStrings.LineNumbersMarginName ? "Zeilennummern" : null,
        }))
        {
            DiffPanePresenter pane = new();
            Assert.Equal("Zeilennummern", AutomationProperties.GetName(pane.LineNumberMargin));
            Assert.Equal("Change markers", AutomationProperties.GetName(pane.ChangeMarkerMargin));
        }
    }

    /// <summary>The first 1-based line of <paramref name="side"/> with padding above it.</summary>
    internal static int FirstPaddedLine(PresenterHost host, DiffSide side)
    {
        int count = host.Document.Pane(side).Lines.Count;
        for (int line = 0; line < count; line++)
        {
            if (Padding.Before(host.Document, side, line) > 0)
            {
                return line + 1;
            }
        }

        throw new InvalidOperationException($"{side} has no padded line in this fixture.");
    }

    private static void AssertTextRendered(PresenterHost host, DiffPanePresenter pane, WriteableBitmap frame)
    {
        Point origin = host.ToWindow(pane, new Point(0, 0));
        double lineHeight = pane.TextArea.TextView.DefaultLineHeight;
        PixelRect firstRow = PixelProbe.Inside(origin.X, origin.Y, origin.X + 150, origin.Y + lineHeight);
        Assert.True(PixelProbe.Count(frame, firstRow, PixelProbe.IsDark) > 0, "line 1's glyphs should still render");
    }

    private sealed class ThrowingRenderer(DiffPanePresenter owner) : GuardedBackgroundRenderer(owner, KnownLayer.Background, "ThrowingRenderer")
    {
        public int Draws { get; private set; }

        protected override void DrawCore(TextView textView, DrawingContext drawingContext)
        {
            Draws++;
            throw new InvalidOperationException("deliberate renderer fault");
        }
    }
}
