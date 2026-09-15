using System.Reflection;
using System.Text.RegularExpressions;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// The checks plan 00019 puts in front of <c>docs/hosting-diffview.md</c>: every API name the guide
/// prints resolves against the shipped surface, and the quickstart it tells a stranger to paste
/// parses, declares the namespace the library actually has, and runs.
/// </summary>
/// <remarks>
/// Split from the tests the way <c>LocaleReview</c> is, so each check can first be shown catching a
/// document broken in exactly that one way before the real document goes through all of them.
/// <para>
/// Nothing backticked and dotted is skipped silently. That is the whole design: a gate that ignored
/// what it could not resolve would repeat the <c>DocumentationCitationTests</c> mistake, where a
/// character class holding no <c>.</c> made 155 qualified citations invisible and one of them named
/// a deleted test for four commits. The two categories that are legitimately not API — package ids
/// and assembly names, which this guide must print because they differ from each other and from the
/// namespace — are read from the build rather than listed here, so they cannot rot.
/// </para>
/// </remarks>
internal static class HostingGuideGate
{
    /// <summary>Where the guide lives, once phase 2 writes it.</summary>
    public const string DocumentPath = "docs/hosting-diffview.md";

    /// <summary>
    /// A backticked dotted token whose first segment is PascalCase — <c>SideBySideDiffView.LeftSource</c>,
    /// <c>Bennewitz.Ninja.DiffView.Avalonia</c>, <c>README.md</c>. A lowercase first segment is not a
    /// citation shape at all (<c>net10.0</c>, <c>avares://…</c>), and spaces cannot cross a match, so
    /// two separate inline spans on one line never join into a third.
    /// </summary>
    private static readonly Regex Dotted = new(@"`([A-Z][A-Za-z0-9]*(?:\.[A-Za-z0-9]+)+)`", RegexOptions.Compiled);

    /// <summary>The <c>using:</c> CLR namespace a quickstart imports, which is what a reader copies.</summary>
    private static readonly Regex UsingNamespace = new(@"xmlns:[A-Za-z0-9]+\s*=\s*""using:([^""]+)""", RegexOptions.Compiled);

    /// <summary>A <c>PackageId</c> as the csproj declares it.</summary>
    private static readonly Regex DeclaredPackageId = new(@"<PackageId>([^<]+)</PackageId>", RegexOptions.Compiled);

    private const BindingFlags PublicMembers =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    /// <summary>What a backticked dotted token turned out to be.</summary>
    public enum Kind
    {
        /// <summary>A type or a member of one. Must resolve.</summary>
        Api,

        /// <summary>A package id. Not a member, and the guide has to print it.</summary>
        PackageId,

        /// <summary>An assembly name. Same.</summary>
        AssemblyName,

        /// <summary>A file: the segment after the last dot is an extension, not a PascalCase member.</summary>
        FileName,
    }

    /// <summary>
    /// The public surface a consumer actually has: both packable assemblies, exported types only —
    /// plus the framework's own, because the guide necessarily names them. <c>Application.Styles</c>
    /// is the instruction a host cannot skip, and holding it to our two assemblies would make the
    /// whole theming section a wall of false failures. Three types reach the three assemblies that
    /// matter: Controls, Base and Markup.Xaml.
    /// </summary>
    public static Type[] Surface() =>
    [
        .. typeof(SideBySideDiffView).Assembly.GetExportedTypes(),
        .. typeof(PaneSource).Assembly.GetExportedTypes(),
        .. typeof(Avalonia.Application).Assembly.GetExportedTypes(),
        .. typeof(Avalonia.Styling.Style).Assembly.GetExportedTypes(),
        .. typeof(Avalonia.Markup.Xaml.Styling.StyleInclude).Assembly.GetExportedTypes(),
    ];

    /// <summary>The two assembly names, from the loaded assemblies rather than from a literal.</summary>
    public static IReadOnlyCollection<string> AssemblyNames() =>
    [
        typeof(SideBySideDiffView).Assembly.GetName().Name!,
        typeof(PaneSource).Assembly.GetName().Name!,
    ];

    /// <summary>
    /// The package ids, read from the csproj that declares them. Read rather than listed so that the
    /// day an id changes again — as it did in <c>3ae46d8</c> — this gate follows it instead of
    /// quietly excusing a stale string.
    /// </summary>
    public static IReadOnlyCollection<string> PackageIds()
    {
        string[] projects =
        [
            Path.Combine("src", "DiffView.Avalonia", "DiffView.Avalonia.csproj"),
            Path.Combine("src", "DiffView.Core", "DiffView.Core.csproj"),
        ];

        List<string> ids = [];
        foreach (string project in projects)
        {
            string text = File.ReadAllText(RepoPaths.Source(project));
            foreach (Match match in DeclaredPackageId.Matches(text))
            {
                ids.Add(match.Groups[1].Value.Trim());
            }
        }

        return ids;
    }

    /// <summary>Decides what a dotted token is, so only the API ones are held to resolving.</summary>
    public static Kind Classify(string dotted, IReadOnlyCollection<string> packageIds, IReadOnlyCollection<string> assemblyNames)
    {
        // Order matters: DiffView.Avalonia is an assembly name and would otherwise read as
        // "member Avalonia of type DiffView", which resolves to nothing and would fail the gate.
        if (packageIds.Contains(dotted, StringComparer.Ordinal))
        {
            return Kind.PackageId;
        }

        if (assemblyNames.Contains(dotted, StringComparer.Ordinal))
        {
            return Kind.AssemblyName;
        }

        string last = dotted[(dotted.LastIndexOf('.') + 1)..];
        return char.IsUpper(last[0]) ? Kind.Api : Kind.FileName;
    }

    /// <summary>
    /// True when the token names a public type, or a public member of one. Non-public members
    /// resolve to absent on purpose: the reader cannot call them, so naming one is the same defect
    /// as naming one that does not exist.
    /// </summary>
    public static bool Resolves(string dotted, Type[] surface)
    {
        if (IsType(dotted, surface))
        {
            return true;
        }

        int dot = dotted.LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        string typeName = dotted[..dot];
        string member = dotted[(dot + 1)..];

        return surface
            .Where(type => Named(type, typeName))
            .Any(type => type.GetMember(member, PublicMembers).Length > 0);
    }

    /// <summary>Every API citation in the document that does not resolve, in the order it appears, once each.</summary>
    public static List<string> Unresolved(string markdown, Type[] surface)
    {
        IReadOnlyCollection<string> packageIds = PackageIds();
        IReadOnlyCollection<string> assemblyNames = AssemblyNames();

        List<string> unresolved = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (Match match in Dotted.Matches(markdown))
        {
            string dotted = match.Groups[1].Value;
            if (Classify(dotted, packageIds, assemblyNames) != Kind.Api)
            {
                continue;
            }

            if (!Resolves(dotted, surface) && seen.Add(dotted))
            {
                unresolved.Add(dotted);
            }
        }

        return unresolved;
    }

    /// <summary>
    /// The content of the first fenced block tagged <paramref name="language"/>. The quickstart under
    /// test is read from the document rather than retyped beside it, because a copy is a thing that
    /// drifts and this one drifting is paid for by someone who cannot fix it.
    /// </summary>
    public static string? FirstBlock(string markdown, string language)
    {
        string opening = "```" + language;
        string[] lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.Equals(lines[i].Trim(), opening, StringComparison.Ordinal))
            {
                continue;
            }

            List<string> body = [];
            for (int j = i + 1; j < lines.Length; j++)
            {
                if (lines[j].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    return string.Join("\n", body);
                }

                body.Add(lines[j]);
            }

            // An unterminated fence is a broken document, not an empty block.
            return null;
        }

        return null;
    }

    /// <summary>The CLR namespace the markup imports, or null when it imports none.</summary>
    public static string? DeclaredNamespace(string xaml)
    {
        Match match = UsingNamespace.Match(xaml);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsType(string name, Type[] surface) => surface.Any(type => Named(type, name));

    private static bool Named(Type type, string name) =>
        string.Equals(type.Name, name, StringComparison.Ordinal)
        || string.Equals(type.FullName, name, StringComparison.Ordinal);
}
