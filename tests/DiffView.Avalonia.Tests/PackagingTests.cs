using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;
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

    /// <summary>
    /// Plan 00019 §Phase 3: the two published packages carry the hosting guide as their readme.
    /// </summary>
    /// <remarks>
    /// Not in <see cref="Required"/>, because <c>src/ThemeAudit</c> is packable too and is
    /// local-feed-only by design; it carries no readme and should not be made to.
    /// </remarks>
    [Fact]
    public void The_published_packages_declare_the_hosting_guide_as_their_readme()
    {
        Assert.True(
            File.Exists(Path.Combine(RepoPaths.Root, "docs", "hosting-diffview.md")),
            "The guide is gone, so the readme both packages declare names nothing and the pack fails.");

        List<string> declaring = PackableProjects()
            .Where(project => PropertiesIn(project).Contains("PackageReadmeFile"))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["DiffView.Avalonia", "DiffView.Core"], declaring);
    }

    /// <summary>
    /// The configuration this test assembly was compiled in, which is the only one whose build
    /// output exists when <c>--no-build</c> runs.
    /// </summary>
    /// <remarks>
    /// Read from the assembly rather than from <c>#if DEBUG</c>: the attribute is what the build
    /// actually recorded, and it is right for any configuration someone adds later.
    /// </remarks>
    private static string BuiltConfiguration =>
        typeof(PackagingTests).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyConfigurationAttribute>()?.Configuration
        ?? "Debug";

    /// <summary>
    /// The readme reaches the package. A <c>PackageReadmeFile</c> naming a file the project does not
    /// actually pack fails <c>dotnet pack</c> outright — the
    /// <c>LayeredEditors.Avalonia.Diagnostics</c> failure recorded in <c>DECISIONS.md</c> — and the
    /// guide sits outside both project directories, so the property alone would do exactly that.
    /// Asserted by packing rather than by reading the csproj, because what the nuspec ends up saying
    /// is the thing nuget.org acts on.
    /// </summary>
    [Fact]
    public void The_readme_each_package_declares_is_inside_the_package()
    {
        string output = Path.Combine(Path.GetTempPath(), "diffview-pack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);

        try
        {
            foreach (string project in PackableProjects()
                         .Where(project => PropertiesIn(project).Contains("PackageReadmeFile")))
            {
                // --no-build: the suite has already built this configuration, and packing again
                // would triple the cost of the check. A failure here prints the pack's own output.
                //
                // ⛔ -c is not optional, and leaving it off is what made this test a false green
                // for five plans. `dotnet pack` defaults to Release; the suite builds Debug. With
                // --no-build the pack then reads bin/Release, which on this machine was populated
                // by the release work of plans 00018 and 00019 and on a clean checkout does not
                // exist at all — NU5026, on every CI machine, from the first run this repository
                // ever had. Named from the assembly rather than hardcoded, so the check stays
                // honest if the suite is ever run in Release.
                (int code, string log) = Run(
                    "dotnet",
                    ["pack", project, "--no-build", "-c", BuiltConfiguration, "-o", output, "-nodeReuse:false"]);
                Assert.True(code == 0, $"dotnet pack failed for {Path.GetFileName(project)}:\n{log}");
            }

            string[] packages = Directory.GetFiles(output, "*.nupkg");
            Assert.Equal(2, packages.Length);

            foreach (string package in packages)
            {
                using ZipArchive archive = ZipFile.OpenRead(package);

                ZipArchiveEntry nuspec = Assert.Single(
                    archive.Entries,
                    entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

                using Stream stream = nuspec.Open();
                XDocument document = XDocument.Load(stream);
                XElement? readme = document.Descendants().FirstOrDefault(
                    element => string.Equals(element.Name.LocalName, "readme", StringComparison.Ordinal));

                string name = Path.GetFileName(package);
                Assert.True(readme is not null, $"{name}'s nuspec names no readme.");
                Assert.Equal("hosting-diffview.md", readme!.Value.Trim());

                Assert.True(
                    archive.Entries.Any(entry => string.Equals(entry.FullName, readme.Value.Trim(), StringComparison.Ordinal)),
                    $"{name}'s nuspec names {readme.Value.Trim()}, which is not in the package.");
            }
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static (int Code, string Output) Run(string file, string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = file,
            WorkingDirectory = RepoPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {file}.");

        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
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
