using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 10, item 1: what the composite costs at scale — the 200,000-line pair and
/// the one-megabyte single line, from the build through the first frame to a scroll at either
/// end. Numbers are written to the test output and recorded in <c>PROGRESS.md</c>; only the
/// invariants are asserted, because a stopwatch is a property of the machine.
/// </summary>
[Trait("Category", "Perf")]
public sealed class ScalePerfTests(ITestOutputHelper output)
{
    /// <summary>A pair of <paramref name="lines"/> lines with one line modified and one inserted every fifty.</summary>
    private static (string Left, string Right) LargePair(int lines)
    {
        StringBuilder left = new(lines * 12);
        StringBuilder right = new(lines * 12);
        for (int i = 0; i < lines; i++)
        {
            left.Append("line ").Append(i).Append('\n');
            if (i % 50 == 10)
            {
                right.Append("LINE ").Append(i).Append('\n');
                right.Append("inserted after ").Append(i).Append('\n');
            }
            else
            {
                right.Append("line ").Append(i).Append('\n');
            }
        }

        return (left.ToString(), right.ToString());
    }

    [AvaloniaFact]
    public async Task The_200k_line_pair_builds_primes_paints_and_scrolls()
    {
        (string left, string right) = LargePair(200_000);
        using CompositeHost host = new(width: 1200, height: 800);
        // The real builder, not the host's time-zeroing one: this test wants the number.
        host.View.Builder = static (l, r, options, token) => DiffDocumentBuilder.Build(l, r, options, token);
        host.Show();

        Stopwatch stopwatch = Stopwatch.StartNew();
        await host.LoadAsync(left, right);
        TimeSpan loaded = stopwatch.Elapsed;

        stopwatch.Restart();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        TimeSpan firstPaint = stopwatch.Elapsed;

        SideBySideDocument document = host.View.Document!;
        double lineHeight = host.Left.TextArea.TextView.DefaultLineHeight;
        TimeSpan middle = Scroll(host, document.Rows.Count / 2 * lineHeight);
        TimeSpan end = Scroll(host, document.Rows.Count * lineHeight);
        TimeSpan back = Scroll(host, 0);

        output.WriteLine(
            $"200k pair ({document.Rows.Count:N0} rows, {document.Blocks.Count:N0} blocks): "
            + $"build {host.View.Diagnostics!.BuildTime.TotalMilliseconds:F0} ms (engine), "
            + $"load through prime and layout {loaded.TotalMilliseconds:F0} ms, "
            + $"first frame {firstPaint.TotalMilliseconds:F0} ms, "
            + $"scroll to middle {middle.TotalMilliseconds:F0} ms, to end {end.TotalMilliseconds:F0} ms, back to top {back.TotalMilliseconds:F0} ms; "
            + $"primed {host.Left.PrimedLineCount:N0} left / {host.Right.PrimedLineCount:N0} right lines");

        Assert.True(document.Rows.Count > 200_000);
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.False(host.Left.IsDegraded);
        Assert.False(host.Right.IsDegraded);
        Assert.Equal(host.Left.TextArea.TextView.DocumentHeight, host.Right.TextArea.TextView.DocumentHeight, 3);
    }

    [AvaloniaFact]
    public async Task The_one_megabyte_single_line_renders_and_scrolls_sideways()
    {
        const int size = 1_000_000;
        StringBuilder builder = new(size + 16);
        while (builder.Length < size)
        {
            builder.Append("lorem ipsum dolor ");
        }

        builder.Length = size;
        string left = builder.ToString();
        string right = left[..^5] + "amet!";

        using CompositeHost host = new(width: 1200, height: 400);
        host.View.Builder = static (l, r, options, token) => DiffDocumentBuilder.Build(l, r, options, token);
        host.View.SyncHorizontalScroll = true;
        host.Show();

        Stopwatch stopwatch = Stopwatch.StartNew();
        await host.LoadAsync(left, right);
        TimeSpan loaded = stopwatch.Elapsed;

        stopwatch.Restart();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        TimeSpan firstPaint = stopwatch.Elapsed;

        stopwatch.Restart();
        host.Left.PaneScrollViewer!.Offset = new Vector(host.Left.PaneScrollViewer.Extent.Width / 2, 0);
        CompositeHost.Layout();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        TimeSpan sideways = stopwatch.Elapsed;

        output.WriteLine(
            $"1 MB single line: build {host.View.Diagnostics!.BuildTime.TotalMilliseconds:F0} ms (engine), "
            + $"load through prime and layout {loaded.TotalMilliseconds:F0} ms, "
            + $"first frame {firstPaint.TotalMilliseconds:F0} ms, "
            + $"scroll to the middle of the line {sideways.TotalMilliseconds:F0} ms");

        Assert.Equal(1, host.Left.Document.LineCount);
        Assert.False(host.Left.IsDegraded);
        Assert.Contains(host.View.Warnings, w => w.Code == DiffWarningCode.LongLinesSkipped);
    }

    /// <summary>Scrolls both panes to <paramref name="offset"/> and returns what the frame cost.</summary>
    private static TimeSpan Scroll(CompositeHost host, double offset)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        host.Right.PaneScrollViewer!.Offset = new Vector(0, offset);
        CompositeHost.Layout();
        using WriteableBitmap _ = host.Capture();
        return stopwatch.Elapsed;
    }
}
