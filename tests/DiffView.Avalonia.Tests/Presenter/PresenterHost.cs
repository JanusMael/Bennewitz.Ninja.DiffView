using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Presenter;

/// <summary>
/// A window with two <see cref="DiffPanePresenter"/>s side by side, both fed from one Core build
/// of a pair, under the test application's theme. No scroll sync: that is Phase 5. The panes use
/// the bundled monospace font through the test application's override of the font token and
/// keep the caret from blinking so pixel assertions are stable.
/// </summary>
internal sealed class PresenterHost : IDisposable
{
    public const double FontSize = 14;
    public const double Tolerance = 1e-6;

    public PresenterHost(string leftText, string rightText, double width = 700, double height = 320, DiffOptions? options = null)
    {
        Result = DiffDocumentBuilder.Build(leftText, rightText, CancellationToken.None, options);
        Window = new Window { Width = width, Height = height };
        Left = CreatePresenter(DiffSide.Left, leftText, Result.Document);
        Right = CreatePresenter(DiffSide.Right, rightText, Result.Document);

        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("*,*") };
        Grid.SetColumn(Right, 1);
        grid.Children.Add(Left);
        grid.Children.Add(Right);
        Window.Content = grid;
    }

    public DiffBuildResult Result { get; }

    public SideBySideDocument Document => Result.Document;

    public Window Window { get; }

    public DiffPanePresenter Left { get; }

    public DiffPanePresenter Right { get; }

    /// <summary>The committed small pair under <c>fixtures/small</c>.</summary>
    public static (string Left, string Right) SmallFixture()
    {
        string directory = RepoPaths.Source(Path.Combine("fixtures", "small"));
        return (File.ReadAllText(Path.Combine(directory, "left.txt")), File.ReadAllText(Path.Combine(directory, "right.txt")));
    }

    public static PresenterHost Small(double width = 700, double height = 320)
    {
        (string left, string right) = SmallFixture();
        return new PresenterHost(left, right, width, height);
    }

    public static void Layout()
    {
        Dispatcher.UIThread.RunJobs();
    }

    public static ScrollViewer ScrollViewerOf(DiffPanePresenter pane)
    {
        return pane.FindDescendantOfType<ScrollViewer>()
               ?? throw new InvalidOperationException("The presenter template has no ScrollViewer.");
    }

    public static void ScrollTo(DiffPanePresenter pane, double verticalOffset)
    {
        ScrollViewerOf(pane).Offset = new Vector(0, verticalOffset);
        Layout();
    }

    /// <summary>Document-relative top of a line from the height tree: the top of its padding block.</summary>
    public static double TopOfLine(DiffPanePresenter pane, int lineNumber)
    {
        return pane.TextArea.TextView.GetVisualTopByDocumentLine(lineNumber);
    }

    /// <summary><paramref name="over"/> alpha-composited on the opaque <paramref name="under"/>.</summary>
    public static Color Composite(Color over, Color under)
    {
        double alpha = over.A / 255.0;
        byte Blend(byte top, byte bottom) => (byte)Math.Round(top * alpha + bottom * (1 - alpha));
        return Color.FromRgb(Blend(over.R, under.R), Blend(over.G, under.G), Blend(over.B, under.B));
    }

    /// <summary>Whether two colours are within <paramref name="tolerance"/> per channel.</summary>
    public static bool Near(Color a, Color b, int tolerance = 6)
    {
        return Math.Abs(a.R - b.R) <= tolerance && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;
    }

    public DiffPanePresenter Pane(DiffSide side)
    {
        return side == DiffSide.Left ? Left : Right;
    }

    public void Show()
    {
        Window.Show();
        Layout();
    }

    /// <summary>The colour of a <c>DiffView.*</c> brush token under the application's actual variant.</summary>
    public static Color Token(string key)
    {
        Application app = Application.Current!;
        Assert.True(app.TryGetResource(key, app.ActualThemeVariant, out object? value), $"{key} did not resolve");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    /// <summary>Document-relative top of a line's own row: past the padding above it.</summary>
    public double RowTopOfLine(DiffPanePresenter pane, int lineNumber)
    {
        double lineHeight = pane.TextArea.TextView.DefaultLineHeight;
        return TopOfLine(pane, lineNumber) + Padding.Before(Document, pane.Side, lineNumber - 1) * lineHeight;
    }

    /// <summary>Every pair of lines that share a row sits at the same document-relative top.</summary>
    public void AssertRowsAligned()
    {
        foreach (AlignedRow row in Document.Rows)
        {
            if (row.LeftLine is { } left && row.RightLine is { } right)
            {
                Assert.Equal(RowTopOfLine(Left, left + 1), RowTopOfLine(Right, right + 1), Tolerance);
            }
        }
    }

    /// <summary>A point in a pane's text-view coordinates translated to the window, for input and pixel sampling.</summary>
    public Point ToWindow(DiffPanePresenter pane, Point textViewPoint)
    {
        return pane.TextArea.TextView.TranslatePoint(textViewPoint, Window)
               ?? throw new InvalidOperationException("The text view is not in the window's visual tree.");
    }

    /// <summary>A point in a margin's coordinates translated to the window.</summary>
    public Point ToWindow(Control control, Point point)
    {
        return control.TranslatePoint(point, Window)
               ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
    }

    public WriteableBitmap Capture()
    {
        Layout();
        return Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
    }

    public void Dispose()
    {
        Window.Close();
    }

    private static DiffPanePresenter CreatePresenter(DiffSide side, string text, SideBySideDocument document)
    {
        return new DiffPanePresenter
        {
            Side = side,
            Document = new TextDocument(text),
            DiffDocument = document,
            FontSize = FontSize,
            IsCaretBlinkEnabled = false,
        };
    }
}
