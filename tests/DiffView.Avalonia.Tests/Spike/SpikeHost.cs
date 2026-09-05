using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Spike;

// Phase 1 spike, throwaway.

/// <summary>
/// A window with two plain <see cref="TextEditor"/>s side by side, each carrying one side of an
/// <see cref="AlignmentFixture"/> and a <see cref="PaddingGenerator"/> for that side's padding.
/// AvaloniaEdit's editor and text-area control themes are merged into the window (its theme
/// files for Fluent and Simple pull static resources Semi does not define). No scrollbars are
/// shown, so the text view fills its editor and both panes have the same viewport.
/// </summary>
internal sealed class SpikeHost : IDisposable
{
    public const double FontSize = 14;

    public SpikeHost(AlignmentFixture fixture, double width = 600, double height = 300)
    {
        Fixture = fixture;
        Window = new Window { Width = width, Height = height };
        // Base.xaml merges the editor, text-area, completion and search-panel dictionaries at
        // compile time; the individual files are not addressable by URI.
        Uri baseUri = new("avares://DiffView.Avalonia.Tests/");
        Window.Resources.MergedDictionaries.Add(new ResourceInclude(baseUri) { Source = new Uri("avares://AvaloniaEdit/Themes/Base.xaml") });

        Left = CreateEditor(fixture.LeftText, fixture.LeftPadding);
        Right = CreateEditor(fixture.RightText, fixture.RightPadding);
        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("*,*") };
        Grid.SetColumn(Right, 1);
        grid.Children.Add(Left);
        grid.Children.Add(Right);
        Window.Content = grid;
    }

    public AlignmentFixture Fixture { get; }

    public Window Window { get; }

    public TextEditor Left { get; }

    public TextEditor Right { get; }

    public void Show()
    {
        Window.Show();
        Layout();
    }

    public static void Layout()
    {
        Dispatcher.UIThread.RunJobs();
    }

    public IReadOnlyDictionary<int, PaddingSpec> PaddingOf(TextEditor editor)
    {
        return ReferenceEquals(editor, Left) ? Fixture.LeftPadding : Fixture.RightPadding;
    }

    /// <summary>Primes every padded line of the editor and runs the layout pass that publishes the extent.</summary>
    public int Prime(TextEditor editor, int batchSize = int.MaxValue)
    {
        int built = PaddingPrimer.Prime(editor.TextArea.TextView, PaddingOf(editor).Keys, batchSize);
        Layout();
        return built;
    }

    public static ScrollViewer ScrollViewerOf(TextEditor editor)
    {
        return editor.FindDescendantOfType<ScrollViewer>()
               ?? throw new InvalidOperationException("The editor template has no ScrollViewer; is the AvaloniaEdit theme merged?");
    }

    public static void ScrollTo(TextEditor editor, double verticalOffset)
    {
        ScrollViewerOf(editor).Offset = new Vector(0, verticalOffset);
        Layout();
    }

    /// <summary>Document-relative top of a line, from the height tree.</summary>
    public static double TopOfLine(TextEditor editor, int lineNumber)
    {
        return editor.TextArea.TextView.GetVisualTopByDocumentLine(lineNumber);
    }

    /// <summary>A point in text-view coordinates translated to the window, for headless input and pixel sampling.</summary>
    public Point ToWindow(TextEditor editor, Point textViewPoint)
    {
        return editor.TextArea.TextView.TranslatePoint(textViewPoint, Window)
               ?? throw new InvalidOperationException("The text view is not in the window's visual tree.");
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

    private static TextEditor CreateEditor(string text, IReadOnlyDictionary<int, PaddingSpec> padding)
    {
        TextEditor editor = new()
        {
            Document = new TextDocument(text),
            FontFamily = new FontFamily(TestFonts.MonoFamilyName),
            FontSize = FontSize,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            ShowLineNumbers = false,
            WordWrap = false,
            // Hidden keeps scrolling enabled (Disabled would turn on word wrap) without a bar.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            // A fresh options object: the styled property's default instance is shared.
            Options = new TextEditorOptions { AllowScrollBelowDocument = false, EnableVirtualSpace = false },
        };

        TextView textView = editor.TextArea.TextView;
        textView.ElementGenerators.Add(new PaddingGenerator(line => padding.GetValueOrDefault(line)));
        return editor;
    }
}
