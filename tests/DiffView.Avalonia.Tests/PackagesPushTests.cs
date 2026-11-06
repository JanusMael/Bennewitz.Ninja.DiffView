using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// <c>packages.push</c> says what this repository publishes: exactly what the solution packs, and exactly
/// what each of the release's two publishing steps names.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A published id cannot be withdrawn, only unlisted</b>, so a project made packable and left out of
/// the file, or an id the file names and the release does not push, is found here and not on nuget.org.
/// <c>scripts/repo-conventions.cs</c> reads the file for one thing, whether it names an id at all.
/// </para>
/// <para>
/// ⚠ <b>The release still names its ids rather than reading this file</b>, which <c>DECISIONS.md</c> says
/// why: it has never run, and this repository's move into AppKit replaces it with the template's. So three
/// places name them, and this test is what makes them one fact. Compared as sets, because the order of
/// the file says nothing.
/// </para>
/// </remarks>
public sealed partial class PackagesPushTests
{
    private const string PushStep = "Push to NuGet.org";

    private const string ReleaseStep = "Create GitHub Release";

    [Fact]
    public void The_file_names_exactly_what_the_solution_packs_and_the_release_publishes()
    {
        string[] pushed = PushedIds();
        string[] packed = [.. PackagingTests.PackableProjects().Select(PackageIdOf).Order(StringComparer.Ordinal)];

        Assert.True(
            packed.Length > 0,
            "The reading found no packable project under src, so there is nothing to hold packages.push to.");

        Assert.True(
            packed.SequenceEqual(pushed),
            $"The solution packs {string.Join(", ", packed)}, and packages.push names {string.Join(", ", pushed)}. "
            + "Classify every packable project: a published id cannot be withdrawn.");

        string[] release = File.ReadAllLines(Path.Combine(RepoPaths.Root, ".github", "workflows", "release.yml"));
        foreach (string step in (string[])[PushStep, ReleaseStep])
        {
            string[] named = IdsNamedBy(release, step);
            Assert.True(
                named.Length > 0,
                $"release.yml has no step '{step}' naming a package, so nothing is held to packages.push there.");

            Assert.True(
                named.SequenceEqual(pushed),
                $"release.yml's step '{step}' names {string.Join(", ", named)}, and packages.push names "
                + $"{string.Join(", ", pushed)}. The release publishes what the file says.");
        }
    }

    /// <summary>The ids <c>packages.push</c> names, read as <c>repo-conventions</c> reads them, in ordinal order.</summary>
    private static string[] PushedIds() =>
    [
        .. File.ReadLines(Path.Combine(RepoPaths.Root, "packages.push"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>A project's <c>PackageId</c>, or the assembly name it falls back to where it declares none.</summary>
    private static string PackageIdOf(string project) =>
        XDocument.Load(project).Descendants("PackageId").FirstOrDefault()?.Value.Trim()
        ?? Path.GetFileNameWithoutExtension(project);

    /// <summary>The ids a step of the workflow names by their exact files, in ordinal order.</summary>
    private static string[] IdsNamedBy(string[] workflow, string step) =>
    [
        .. workflow
            .SkipWhile(line => line.Trim() != $"- name: {step}")
            .Skip(1)
            .TakeWhile(line => !line.TrimStart().StartsWith("- name:", StringComparison.Ordinal))
            .Select(line => PackageFile().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .Order(StringComparer.Ordinal),
    ];

    [GeneratedRegex(@"\./packages/Release/(?<id>\S+?)\.\$\{\{ env\.VERSION \}\}\.nupkg")]
    private static partial Regex PackageFile();
}
