using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Plan 00018 §Phase 1: a package nobody can take is a package nobody takes.
/// </summary>
/// <remarks>
/// <para>
/// Every one of these defaults to something wrong rather than to nothing, which is why they need a
/// test rather than a reviewer. <c>PackageId</c> falls back to the assembly name, so the library
/// would have published unprefixed; <c>Authors</c> falls back to the same, so the first pack of this
/// repository authored itself as <em>DiffView.Avalonia</em>; and a package with no licence
/// expression is one no compliance review passes, which is not a build error anywhere.
/// </para>
/// <para>
/// Read as XML rather than searched as text. The properties may sit in the project or in
/// <c>Directory.Build.props</c> — both are legitimate — and a substring search would also be
/// satisfied by the comment above them, which names every one of them while defining none.
/// </para>
/// </remarks>
public sealed class PackagingTests
{
    private static readonly string[] Required =
    [
        "PackageId",
        "Authors",
        "PackageProjectUrl",
        "RepositoryUrl",
        "PackageTags",
    ];

    /// <summary>A licence may be an expression or a bundled file; one of the two must be there.</summary>
    private static readonly string[] Licence = ["PackageLicenseExpression", "PackageLicenseFile"];

    [Fact]
    public void Every_packable_project_carries_the_metadata_a_consumer_needs()
    {
        HashSet<string> shared = PropertiesIn(Path.Combine(RepoPaths.Root, "Directory.Build.props"));
        List<string> projects = PackableProjects();

        Assert.NotEmpty(projects);
        List<string> missing = [];

        foreach (string project in projects)
        {
            HashSet<string> declared = [.. shared, .. PropertiesIn(project)];
            string name = Path.GetFileNameWithoutExtension(project);

            foreach (string property in Required)
            {
                if (!declared.Contains(property))
                {
                    missing.Add($"{name}: {property}");
                }
            }

            if (!Licence.Any(declared.Contains))
            {
                missing.Add($"{name}: {string.Join(" or ", Licence)}");
            }
        }

        Assert.True(
            missing.Count == 0,
            "These packable projects would publish without metadata a consumer needs, and every one of them "
            + "defaults to something wrong rather than failing the build: " + string.Join("; ", missing));
    }

    [Fact]
    public void The_licence_the_packages_declare_is_the_one_in_the_repository()
    {
        string licence = Path.Combine(RepoPaths.Root, "LICENSE");
        Assert.True(File.Exists(licence), "There is no LICENSE file, so the declared expression describes nothing.");

        string text = File.ReadAllText(licence);
        HashSet<string> shared = PropertiesIn(Path.Combine(RepoPaths.Root, "Directory.Build.props"));

        // The expression and the file are two independent statements of the same fact, and nothing
        // else compares them: a repository can declare MIT and ship Apache without a murmur.
        Assert.Contains("PackageLicenseExpression", shared, StringComparer.Ordinal);
        Assert.Contains("MIT License", text, StringComparison.Ordinal);
        Assert.Contains("Brian Bennewitz", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Plan 00018 §Phase 2: the release workflow names the packages it pushes, and never globs.
    /// </summary>
    /// <remarks>
    /// An unusual thing to assert about YAML, and the asymmetry earns it. <c>dotnet pack</c> over
    /// this solution produces three packages, because <c>src/ThemeAudit</c> is packable too and is
    /// local-feed-only by design — <c>NuGet.config</c> maps that exact id to <c>../nuget-local</c>.
    /// The workflow this one was modelled on pushes <c>*.nupkg</c>; copying that glob would publish
    /// ThemeAudit to nuget.org on the first release, and a published id cannot be withdrawn, only
    /// unlisted. Silent, instant and permanent against twenty lines of test.
    /// </remarks>
    [Fact]
    public void The_release_workflow_names_the_packages_it_pushes()
    {
        string path = Path.Combine(RepoPaths.Root, ".github", "workflows", "release.yml");
        Assert.True(File.Exists(path), "There is no release workflow, so nothing publishes — and nothing gates what would.");

        // Comment lines are dropped first: the comment above the push step explains the very glob
        // this test forbids, and a whole-file search would fail on the explanation.
        string instructions = string.Join(
            '\n',
            File.ReadLines(path).Where(line => !line.TrimStart().StartsWith('#')));

        Assert.DoesNotContain("*.nupkg", instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemeAudit", instructions, StringComparison.Ordinal);
        Assert.Contains("Bennewitz.Ninja.DiffView.Core.", instructions, StringComparison.Ordinal);
        Assert.Contains("Bennewitz.Ninja.DiffView.Avalonia.", instructions, StringComparison.Ordinal);
    }

    /// <summary>
    /// The version reaches the pack. Without it a release publishes <c>1.0.0</c> — a valid package
    /// at a version that says nothing, and one that can never be published again.
    /// </summary>
    [Fact]
    public void The_release_workflow_carries_the_tag_version_into_build_and_pack()
    {
        string path = Path.Combine(RepoPaths.Root, ".github", "workflows", "release.yml");
        string[] lines = [.. File.ReadLines(path).Where(line => !line.TrimStart().StartsWith('#'))];
        string instructions = string.Join('\n', lines);

        // The tag is the only source of the version, and --no-build on the pack means build and
        // pack must be told the same thing or the pack quietly ships what the build made.
        Assert.Contains("GITHUB_REF_NAME#v", instructions, StringComparison.Ordinal);
        Assert.Equal(2, lines.Count(line => line.Contains("/p:Version=", StringComparison.Ordinal)));
    }

    private static List<string> PackableProjects()
    {
        List<string> packable = [];
        foreach (string project in Directory.GetFiles(Path.Combine(RepoPaths.Root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            if (project.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || project.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            XDocument document = XDocument.Load(project);
            bool isPackable = document.Descendants("PropertyGroup")
                .Elements("IsPackable")
                .Any(e => string.Equals(e.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

            if (isPackable)
            {
                packable.Add(project);
            }
        }

        return packable;
    }

    private static HashSet<string> PropertiesIn(string path)
    {
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return XDocument.Load(path)
            .Descendants("PropertyGroup")
            .Elements()
            .Select(e => e.Name.LocalName)
            .ToHashSet(StringComparer.Ordinal);
    }
}
