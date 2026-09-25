using System.Diagnostics;
using System.Text;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00003 §Phase 6: what an edit costs at scale. Phase 2 shipped `LiveReDiff` on an
/// assumption — that a debounced rebuild might be unaffordable on a large pair — and this is the
/// measurement that settles it. Numbers go to the test output and into <c>PROGRESS.md</c>; only
/// the invariants are asserted, because a stopwatch is a property of the machine.
/// </summary>
[Trait("Category", "Perf")]
public sealed class EditScalePerfTests(ITestOutputHelper output)
{
    /// <summary>The same shape as the Phase 10 fixture: one line modified and one inserted every fifty.</summary>
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
    public async Task An_edit_to_the_200k_pair_re_diffs_re_primes_and_repaints()
    {
        (string left, string right) = LargePair(200_000);
        using CompositeHost host = new(width: 1200, height: 800);
        host.View.Builder = static (l, r, options, token) => DiffDocumentBuilder.Build(l, r, token, options);
        host.Show();
        await host.LoadAsync(left, right);
        using (WriteableBitmap _ = host.Capture())
        {
        }

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();

        // The keystroke itself, with no rebuild yet: this is what the user feels while typing.
        Stopwatch stopwatch = Stopwatch.StartNew();
        host.Window.KeyTextInput("X");
        CompositeHost.Layout();
        TimeSpan keystroke = stopwatch.Elapsed;

        stopwatch.Restart();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        TimeSpan frameWhileStale = stopwatch.Elapsed;

        // Then the debounce fires and the whole pipeline runs again.
        stopwatch.Restart();
        await host.WaitForReDiffAsync();
        TimeSpan reDiff = stopwatch.Elapsed;

        stopwatch.Restart();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        TimeSpan frameAfter = stopwatch.Elapsed;

        SideBySideDocument document = host.View.Document!;
        output.WriteLine(
            $"200k pair, one keystroke: keystroke {keystroke.TotalMilliseconds:F0} ms, "
            + $"frame while the model is stale {frameWhileStale.TotalMilliseconds:F0} ms, "
            + $"re-diff through prime and layout {reDiff.TotalMilliseconds:F0} ms "
            + $"(engine {host.View.Diagnostics!.BuildTime.TotalMilliseconds:F0} ms), "
            + $"frame after {frameAfter.TotalMilliseconds:F0} ms; "
            + $"{document.Rows.Count:N0} rows, {document.Blocks.Count:N0} blocks, "
            + $"primed {host.Left.PrimedLineCount:N0} left / {host.Right.PrimedLineCount:N0} right lines");

        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.True(host.View.IsEdited(DiffSide.Left));
        Assert.False(host.Left.IsDegraded);
        Assert.False(host.Right.IsDegraded);
        Assert.Equal(host.Left.TextArea.TextView.DocumentHeight, host.Right.TextArea.TextView.DocumentHeight, 3);
    }

    [AvaloniaFact]
    public async Task Typing_a_run_of_keys_coalesces_into_one_rebuild_at_scale()
    {
        (string left, string right) = LargePair(200_000);
        using CompositeHost host = new(width: 1200, height: 800);
        host.View.Builder = static (l, r, options, token) => DiffDocumentBuilder.Build(l, r, token, options);
        host.Show();
        await host.LoadAsync(left, right);
        int versionBefore = host.View.Document!.Version;

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();

        // Twenty keystrokes inside the window: the cost that matters is the typing, not the
        // rebuilding, because the debounce should have collapsed twenty rebuilds into one.
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            host.Window.KeyTextInput("x");
            CompositeHost.Layout();
            host.Time.Advance(host.View.ReDiffDelay - TimeSpan.FromMilliseconds(50));
            CompositeHost.Layout();
        }

        TimeSpan typing = stopwatch.Elapsed;
        Assert.Equal(versionBefore, host.View.Document!.Version);

        stopwatch.Restart();
        await host.WaitForReDiffAsync();
        TimeSpan settle = stopwatch.Elapsed;

        output.WriteLine(
            $"200k pair, 20 keystrokes inside the debounce: typing {typing.TotalMilliseconds:F0} ms "
            + $"({typing.TotalMilliseconds / 20:F1} ms per key), settle {settle.TotalMilliseconds:F0} ms, "
            + $"builds {host.View.Document!.Version - versionBefore}");

        Assert.Equal(versionBefore + 1, host.View.Document!.Version);
        Assert.Equal(DiffViewState.Ready, host.View.State);
    }

    [AvaloniaFact]
    public async Task With_live_re_diff_off_a_keystroke_costs_nothing_beyond_the_keystroke()
    {
        (string left, string right) = LargePair(200_000);
        using CompositeHost host = new(width: 1200, height: 800);
        host.View.Builder = static (l, r, options, token) => DiffDocumentBuilder.Build(l, r, token, options);
        host.Show();
        await host.LoadAsync(left, right);
        int versionBefore = host.View.Document!.Version;

        host.View.LiveReDiff = false;
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            host.Window.KeyTextInput("y");
            CompositeHost.Layout();
        }

        host.Time.Advance(host.View.ReDiffDelay * 4);
        CompositeHost.Layout();
        TimeSpan typing = stopwatch.Elapsed;

        Assert.Equal(versionBefore, host.View.Document!.Version);

        stopwatch.Restart();
        host.View.ReDiffNow();
        await host.WaitForBuildAsync();
        TimeSpan explicitBuild = stopwatch.Elapsed;

        output.WriteLine(
            $"200k pair, LiveReDiff off: 20 keystrokes {typing.TotalMilliseconds:F0} ms "
            + $"({typing.TotalMilliseconds / 20:F1} ms per key), explicit ReDiffNow {explicitBuild.TotalMilliseconds:F0} ms");

        Assert.NotEqual(versionBefore, host.View.Document!.Version);
        Assert.Equal(DiffViewState.Ready, host.View.State);
    }
}
