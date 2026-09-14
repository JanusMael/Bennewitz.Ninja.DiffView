using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests.Composite;

/// <summary>
/// Plan 00001 §Phase 9: the grammar chosen from the file name, the theme following the variant,
/// the toggle, and a grammar that will not install — which degrades the control, names the
/// grammar and leaves the diff highlighting alone.
/// </summary>
public sealed class SyntaxTests
{
    /// <summary>The first line of the fixture, <c>using System;</c>: keywords, so a grammar colours it.</summary>
    private const int KeywordLine = 1;

    [AvaloniaFact]
    public async Task An_extension_no_grammar_claims_leaves_plain_text_and_the_state_stays_ready()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(syntax: true);
        host.Show();
        await host.LoadAsync(CompositeHost.Named(left, "left.txt"), CompositeHost.Named(right, "right.txt"));
        using WriteableBitmap frame = host.Capture();

        Assert.Null(host.Left.SyntaxLanguageId);
        Assert.Null(host.Right.SyntaxLanguageId);
        Assert.Equal(1, SyntaxProbe.Foregrounds(host.Left, KeywordLine));
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.False(host.Left.IsDegraded);
        Assert.NotEmpty(host.Left.BackgroundRenderer.LastDrawn);
    }

    [AvaloniaFact]
    public async Task A_source_with_no_name_at_all_stays_plain_text()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(syntax: true);
        host.Show();
        await host.LoadAsync(left, right);
        using WriteableBitmap frame = host.Capture();

        Assert.Null(host.Left.SyntaxLanguageId);
        Assert.Equal(DiffViewState.Ready, host.View.State);
    }

    [AvaloniaFact]
    public async Task The_grammar_follows_the_path_when_the_source_has_one()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new(syntax: true);
        host.Show();
        await host.LoadAsync(
            new PaneSource(left) { Path = Path.Combine("sample", "Greeter.cs"), Title = "Greeter.cs" },
            new PaneSource(right) { Path = Path.Combine("sample", "Greeter.cs"), Title = "Greeter.cs" });

        Assert.True(await CompositeHost.PumpUntilAsync(() => SyntaxProbe.IsColoured(host.Left, KeywordLine)));
        Assert.Equal("csharp", host.Left.SyntaxLanguageId);
        Assert.Equal("csharp", host.Right.SyntaxLanguageId);
    }

    [AvaloniaFact]
    public async Task A_csharp_pair_is_colourised_in_both_panes_and_the_row_kinds_are_still_drawn()
    {
        (PaneSource left, PaneSource right) = CompositeHost.CsharpFixture();
        using CompositeHost host = new(syntax: true);
        host.Show();
        await host.LoadAsync(left, right);

        Assert.True(await CompositeHost.PumpUntilAsync(
            () => SyntaxProbe.IsColoured(host.Left, KeywordLine) && SyntaxProbe.IsColoured(host.Right, KeywordLine)));
        using WriteableBitmap frame = host.Capture();

        Assert.Equal("csharp", host.Left.SyntaxLanguageId);
        Assert.Equal("csharp", host.Right.SyntaxLanguageId);
        Assert.Equal(DiffViewState.Ready, host.View.State);
        Assert.False(host.Left.IsDegraded);
        Assert.False(host.Right.IsDegraded);

        // The diff layer is untouched: every row still carries the kind the model gave it.
        Assert.Contains(host.Right.BackgroundRenderer.LastDrawn, line => line.Kind == DiffLineKind.Inserted);
        Assert.Contains(host.Left.BackgroundRenderer.LastDrawn, line => line.Kind == DiffLineKind.Modified);
    }

    [AvaloniaFact]
    public async Task The_syntax_theme_follows_the_variant()
    {
        Application app = Application.Current!;
        ThemeVariant? previous = app.RequestedThemeVariant;
        try
        {
            app.RequestedThemeVariant = ThemeVariant.Light;
            (PaneSource left, PaneSource right) = CompositeHost.CsharpFixture();
            using CompositeHost host = new(syntax: true);
            host.Show();
            await host.LoadAsync(left, right);
            Assert.True(await CompositeHost.PumpUntilAsync(() => SyntaxProbe.IsColoured(host.Left, KeywordLine)));

            // The first token's own colour, which the grammar's theme sets and the diff palette
            // does not — so this changes only if the syntax theme followed the variant.
            Color? light = SyntaxProbe.FirstColour(host.Left, KeywordLine);
            Assert.NotNull(light);

            app.RequestedThemeVariant = ThemeVariant.Dark;
            Assert.True(
                await CompositeHost.PumpUntilAsync(() => SyntaxProbe.FirstColour(host.Left, KeywordLine) != light),
                "the keyword colour should follow the variant");
            Assert.True(SyntaxProbe.IsColoured(host.Left, KeywordLine));
            Assert.Equal("csharp", host.Left.SyntaxLanguageId);
        }
        finally
        {
            app.RequestedThemeVariant = previous;
        }
    }

    [AvaloniaFact]
    public async Task Turning_the_toggle_off_returns_the_panes_to_plain_text_and_turning_it_on_colours_them_again()
    {
        (PaneSource left, PaneSource right) = CompositeHost.CsharpFixture();
        using CompositeHost host = new(syntax: true);
        host.Show();
        await host.LoadAsync(left, right);
        Assert.True(await CompositeHost.PumpUntilAsync(() => SyntaxProbe.IsColoured(host.Left, KeywordLine)));

        host.View.UseSyntaxHighlighting = false;
        CompositeHost.Layout();
        using (WriteableBitmap _ = host.Capture())
        {
        }

        Assert.Null(host.Left.SyntaxLanguageId);
        Assert.Null(host.Right.SyntaxLanguageId);
        Assert.Equal(1, SyntaxProbe.Foregrounds(host.Left, KeywordLine));

        host.View.UseSyntaxHighlighting = true;
        Assert.True(await CompositeHost.PumpUntilAsync(() => SyntaxProbe.IsColoured(host.Left, KeywordLine)));
        Assert.Equal("csharp", host.Left.SyntaxLanguageId);
        Assert.Equal(DiffViewState.Ready, host.View.State);
    }

    [AvaloniaFact]
    public async Task A_grammar_that_will_not_install_degrades_the_control_names_it_and_leaves_the_diff_highlighting()
    {
        (PaneSource left, PaneSource right) = CompositeHost.CsharpFixture();
        using CompositeHost host = new(syntax: true);
        host.Show();
        List<RenderFaultEventArgs> faults = [];
        host.View.RenderFault += (_, e) => faults.Add(e);
        host.Left.SyntaxInstallerForTesting = _ => throw new InvalidOperationException("the grammar is not there");

        await host.LoadAsync(left, right);
        Assert.True(await CompositeHost.PumpUntilAsync(() => faults.Count > 0 && SyntaxProbe.IsColoured(host.Right, KeywordLine)));
        using WriteableBitmap frame = host.Capture();

        // Once, naming the grammar it could not install, and with the pane left as plain text.
        RenderFaultEventArgs fault = Assert.Single(faults);
        Assert.Equal(nameof(SyntaxHighlighting), fault.Source);
        Assert.Equal("csharp", fault.Subject);
        Assert.Contains("csharp", fault.Message, StringComparison.Ordinal);
        Assert.Null(host.Left.SyntaxLanguageId);
        Assert.Equal(1, SyntaxProbe.Foregrounds(host.Left, KeywordLine));

        Assert.Equal(DiffViewState.Degraded, host.View.State);
        Assert.Contains("csharp", host.View.StateMessage!, StringComparison.Ordinal);

        // The diff is unaffected on both sides, and the other pane is still colourised.
        Assert.Contains(host.Left.BackgroundRenderer.LastDrawn, line => line.Kind == DiffLineKind.Modified);
        Assert.Contains(host.Right.BackgroundRenderer.LastDrawn, line => line.Kind == DiffLineKind.Inserted);
        Assert.Equal("csharp", host.Right.SyntaxLanguageId);
    }
}
