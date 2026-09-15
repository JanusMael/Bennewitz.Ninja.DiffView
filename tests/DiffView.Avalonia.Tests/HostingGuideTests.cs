using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Bennewitz.Ninja.DiffView.Core;
using Bennewitz.Ninja.DiffView.Tests.Composite;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00019 §Phase 1: the gates that land before <c>docs/hosting-diffview.md</c> is written.
/// </summary>
/// <remarks>
/// Each check is first shown catching a document broken in exactly that one way, the way
/// <c>LocaleReviewTests</c> proves its own gate; then the real document — once phase 2 writes it —
/// goes through all of them at once, which is the test that fails the day somebody renames a member
/// the guide names.
/// </remarks>
public sealed class HostingGuideTests
{
    /// <summary>
    /// The snippet a reader pastes. Deliberately loadable as it stands: no <c>x:Class</c>, because a
    /// quickstart that had one could not be driven without a code-behind type, and the gate's whole
    /// claim is that the snippet in the document <em>is</em> the snippet under test rather than a
    /// copy of it that drifts.
    /// </summary>
    private const string Quickstart = """
        <Window xmlns="https://github.com/avaloniaui"
                xmlns:dv="using:Bennewitz.Ninja.DiffView"
                Width="900" Height="600">
          <dv:SideBySideDiffView />
        </Window>
        """;

    /// <summary>
    /// A guide-shaped document, correct unless a parameter breaks exactly one thing about it. Built
    /// by concatenation rather than interpolation so that markup braces never have to be escaped.
    /// </summary>
    private static string Document(string? citation = null, string? xaml = null)
    {
        return "# Hosting DiffView\n\n"
               + "Merge `DiffViewResources.ThemeUri` into `Application.Styles`, or the templates never resolve.\n\n"
               + "```xml\n"
               + (xaml ?? Quickstart) + "\n"
               + "```\n\n"
               + "Assign " + (citation ?? "`SideBySideDiffView.LeftSource`") + " and `SideBySideDiffView.RightSource`; "
               + "the control reaches `DiffViewState.Ready`. It ships as `Bennewitz.Ninja.DiffView.Avalonia` "
               + "over assembly `DiffView.Avalonia`, and `README.md` is the contributor's document.\n";
    }

    private static List<string> Unresolved(string markdown) =>
        HostingGuideGate.Unresolved(markdown, HostingGuideGate.Surface());

    private static HostingGuideGate.Kind Classify(string dotted) =>
        HostingGuideGate.Classify(dotted, HostingGuideGate.PackageIds(), HostingGuideGate.AssemblyNames());

    // ── The citation gate ──────────────────────────────────────────────────────────────────

    [Fact]
    public void A_guide_that_names_only_real_members_trips_nothing()
    {
        Assert.Empty(Unresolved(Document()));
    }

    [Fact]
    public void A_guide_that_names_a_member_that_does_not_exist_is_caught()
    {
        string stale = Assert.Single(Unresolved(Document(citation: "`SideBySideDiffView.NoSuchProperty`")));
        Assert.Equal("SideBySideDiffView.NoSuchProperty", stale);
    }

    [Fact]
    public void A_member_that_exists_but_is_not_public_is_caught()
    {
        // Builder is internal, and this assembly sees it through InternalsVisibleTo. A consumer does
        // not, so naming it in the guide is the same defect as naming one that was never there —
        // which is why the check reflects over exported types and Public binding flags only.
        Assert.NotNull(typeof(SideBySideDiffView)
            .GetProperty("Builder", BindingFlags.NonPublic | BindingFlags.Instance));

        string hidden = Assert.Single(Unresolved(Document(citation: "`SideBySideDiffView.Builder`")));
        Assert.Equal("SideBySideDiffView.Builder", hidden);
    }

    [Fact]
    public void A_framework_type_the_guide_must_name_resolves()
    {
        // Application.Styles is the instruction a host cannot skip. Holding citations to the two
        // DiffView assemblies alone would fail the entire theming section.
        Assert.Empty(Unresolved("Merge it into `Application.Styles` as a `StyleInclude`.\n"));
    }

    // ── What is dotted and backticked but is not a member ──────────────────────────────────

    [Fact]
    public void A_package_id_is_not_read_as_a_member_citation()
    {
        Assert.Equal(HostingGuideGate.Kind.PackageId, Classify("Bennewitz.Ninja.DiffView.Avalonia"));
        Assert.Equal(HostingGuideGate.Kind.PackageId, Classify("Bennewitz.Ninja.DiffView.Core"));
    }

    [Fact]
    public void An_assembly_name_is_not_read_as_a_member_citation()
    {
        Assert.Equal(HostingGuideGate.Kind.AssemblyName, Classify("DiffView.Avalonia"));
        Assert.Equal(HostingGuideGate.Kind.AssemblyName, Classify("DiffView.Core"));
    }

    [Fact]
    public void A_file_name_is_not_read_as_a_member_citation()
    {
        Assert.Equal(HostingGuideGate.Kind.FileName, Classify("README.md"));
        Assert.Equal(HostingGuideGate.Kind.FileName, Classify("DiffView.axaml"));
    }

    [Fact]
    public void The_package_ids_the_gate_excuses_are_the_ones_the_build_declares()
    {
        // Read from the csproj, pinned here. The ids moved once already, in 3ae46d8; this fails on
        // the next move rather than letting the gate excuse a string nothing ships any more.
        Assert.Equal(
            ["Bennewitz.Ninja.DiffView.Avalonia", "Bennewitz.Ninja.DiffView.Core"],
            HostingGuideGate.PackageIds().OrderBy(id => id, StringComparer.Ordinal));
    }

    // ── The quickstart gate ────────────────────────────────────────────────────────────────

    [Fact]
    public void The_quickstart_is_read_from_the_document()
    {
        string? xaml = HostingGuideGate.FirstBlock(Document(), "xml");
        Assert.NotNull(xaml);
        Assert.Contains("SideBySideDiffView", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_with_no_quickstart_block_yields_none()
    {
        Assert.Null(HostingGuideGate.FirstBlock("# Guide\n\nProse only.\n", "xml"));
    }

    [Fact]
    public void The_quickstart_names_the_namespace_the_control_reports()
    {
        string xaml = HostingGuideGate.FirstBlock(Document(), "xml")!;
        Assert.Equal(typeof(SideBySideDiffView).Namespace, HostingGuideGate.DeclaredNamespace(xaml));
    }

    [Fact]
    public void A_quickstart_importing_a_namespace_the_library_does_not_have_is_caught()
    {
        // Precisely the citation plan 00017 would have broken, and the one no reflection over member
        // names would notice: the xmlns is a string, and every member it reaches still exists.
        string moved = Quickstart.Replace(
            "using:Bennewitz.Ninja.DiffView",
            "using:Bennewitz.Ninja.DiffView.Avalonia",
            StringComparison.Ordinal);

        Assert.NotEqual(typeof(SideBySideDiffView).Namespace, HostingGuideGate.DeclaredNamespace(moved));
    }

    [AvaloniaFact]
    public void A_quickstart_whose_xaml_does_not_parse_is_caught()
    {
        string broken = Document(xaml: """
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:dv="using:Bennewitz.Ninja.DiffView">
              <dv:SideBySideDiffView
            </Window>
            """);

        string xaml = HostingGuideGate.FirstBlock(broken, "xml")!;
        Assert.ThrowsAny<Exception>(() => AvaloniaRuntimeXamlLoader.Load(xaml));
    }

    [AvaloniaFact]
    public async Task The_quickstart_parses_and_reaches_ready_over_two_sources()
    {
        string xaml = HostingGuideGate.FirstBlock(Document(), "xml")!;
        await AssertQuickstartRunsAsync(xaml);
    }

    // ── The real document, once it exists ──────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task The_guide_itself_passes_every_check()
    {
        string path = RepoPaths.Source(HostingGuideGate.DocumentPath);
        Assert.SkipUnless(
            File.Exists(path),
            $"{HostingGuideGate.DocumentPath} lands in phase 2. The gate ships first, on purpose.");

        string markdown = await File.ReadAllTextAsync(path);

        List<string> unresolved = HostingGuideGate.Unresolved(markdown, HostingGuideGate.Surface());
        Assert.True(
            unresolved.Count == 0,
            "The guide names API that does not resolve: " + string.Join("; ", unresolved));

        string? xaml = HostingGuideGate.FirstBlock(markdown, "xml");
        Assert.NotNull(xaml);
        Assert.Equal(typeof(SideBySideDiffView).Namespace, HostingGuideGate.DeclaredNamespace(xaml));
        await AssertQuickstartRunsAsync(xaml);
    }

    /// <summary>
    /// Loads the markup, shows it, feeds it two sources and waits for the build. Parsing is not
    /// working: a quickstart that parses and then never leaves <c>Building</c> is still broken for
    /// the reader who pasted it.
    /// </summary>
    private static async Task AssertQuickstartRunsAsync(string xaml)
    {
        Window window = Assert.IsType<Window>(AvaloniaRuntimeXamlLoader.Load(xaml));
        try
        {
            window.Show();
            CompositeHost.Layout();

            SideBySideDiffView view = Assert.Single(
                window.GetLogicalDescendants().OfType<SideBySideDiffView>());

            view.LeftSource = new PaneSource("one\ntwo\nthree\n") { Title = "left.txt" };
            view.RightSource = new PaneSource("one\ntwo changed\nthree\n") { Title = "right.txt" };

            bool ready = await CompositeHost.PumpUntilAsync(() => view.State == DiffViewState.Ready);

            Assert.True(ready, $"The quickstart never reached Ready; it settled at {view.State}.");
            Assert.NotNull(view.Document);
        }
        finally
        {
            window.Close();
        }
    }
}
