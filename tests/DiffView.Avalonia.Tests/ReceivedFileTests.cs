using System.Diagnostics;
using System.Text.Json;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// A gate's rejected output is never a source: no <c>*.received.*</c> file is handed to the markup
/// compiler. <c>ReferenceAuditTests</c> writes <c>&lt;name&gt;.received.axaml</c> beside a stale compat
/// dictionary in <c>src/DiffView.Avalonia/Themes/Compat</c>, and the Avalonia package globs every
/// <c>**/*.axaml</c> under a project, so until the root <c>Directory.Build.props</c> left them out a stray
/// one failed run left there was compiled and embedded — measured, with the build naming it nowhere — and
/// the library would have packed it.
/// </summary>
/// <remarks>
/// <para>
/// Read off MSBuild's own evaluation rather than out of the props file, because the item list is what the
/// markup compiler is handed: an exclusion spelled right where no project imports it, or a glob that stops
/// honouring <c>DefaultItemExcludes</c>, reads correctly in the file and leaves nothing out.
/// </para>
/// <para>
/// Each subject is restored first. Unrestored, a project evaluates without its packages' props and so
/// without Avalonia's glob at all, and reports no markup with exit code 0 — measured in a fresh worktree,
/// which is what <c>mutate-behaviour --parallel</c> cuts, restoring only the set's own test project there.
/// The markup the probe is planted beside is the positive control: a reading that finds no markup finds no
/// probe either.
/// </para>
/// </remarks>
public sealed class ReceivedFileTests
{
    /// <summary>
    /// The item the package's glob fills: <c>CompileAvaloniaXaml</c> compiles it and
    /// <c>GenerateAvaloniaResources</c> embeds it.
    /// </summary>
    private const string MarkupItem = "AvaloniaXaml";

    /// <summary>Every project directly under <c>src</c> that holds markup, by its path from the root.</summary>
    public static TheoryData<string> MarkupProjects
    {
        get
        {
            TheoryData<string> projects = [];
            foreach (string directory in Directory.EnumerateDirectories(RepoPaths.Source("src")).Order(StringComparer.Ordinal))
            {
                string[] found = Directory.GetFiles(directory, "*.csproj");
                if (found.Length == 1 && MarkupUnder(directory).Any())
                {
                    projects.Add(Path.GetRelativePath(RepoPaths.Root, found[0]).Replace('\\', '/'));
                }
            }

            return projects;
        }
    }

    /// <summary>
    /// A probe planted beside a project's markup and named the way a gate names what it rejects is among
    /// no items the project's evaluation hands the markup compiler, while the markup beside it is.
    /// </summary>
    [Theory]
    [MemberData(nameof(MarkupProjects))]
    public void A_received_file_beside_the_markup_is_not_handed_to_the_markup_compiler(string project)
    {
        string path = RepoPaths.Source(project);

        // The first in ordinal order, which for the library is a compat dictionary: where the audit writes.
        // The GUID keeps the probe from ever being a stray a failed run left there for review.
        string markup = MarkupUnder(Path.GetDirectoryName(path)!).Order(StringComparer.Ordinal).First();
        string probe = Path.ChangeExtension(markup, $".{Guid.NewGuid():N}.received.axaml");

        (int restored, string restoreOutput, string restoreError) = Dotnet("restore", path, "-nodeReuse:false");
        Assert.True(restored == 0, $"dotnet restore failed for {project}:\n{restoreOutput}{restoreError}");

        List<string> handed;
        File.WriteAllText(probe, "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" />\n");
        try
        {
            handed = MarkupHandedToTheCompiler(path);
        }
        finally
        {
            File.Delete(probe);
        }

        Assert.True(
            handed.Contains(markup, StringComparer.OrdinalIgnoreCase),
            $"{project} evaluates to no {Path.GetFileName(markup)}, which is on disk beside the probe, so the reading found no markup and its silence about the probe says nothing.");
        Assert.False(
            handed.Contains(probe, StringComparer.OrdinalIgnoreCase),
            $"{project}'s markup glob takes {Path.GetFileName(probe)}, so a received file a failed run leaves beside its markup is compiled and embedded.");
    }

    /// <summary>The full path of every item of <see cref="MarkupItem"/> the project evaluates to.</summary>
    private static List<string> MarkupHandedToTheCompiler(string project)
    {
        (int code, string output, string error) = Dotnet("msbuild", project, $"-getItem:{MarkupItem}", "-nodeReuse:false");
        Assert.True(code == 0, $"dotnet msbuild could not evaluate {project}:\n{output}{error}");

        // An item type with no item, even one the project never defines, still prints as an empty array.
        using JsonDocument evaluation = JsonDocument.Parse(output);
        return evaluation.RootElement.GetProperty("Items").GetProperty(MarkupItem)
            .EnumerateArray()
            .Select(item => item.GetProperty("FullPath").GetString()!)
            .ToList();
    }

    /// <summary>
    /// The markup under a directory that is no received file, the build output and the dot-directories
    /// MSBuild's own globs leave out pruned during the walk rather than filtered after it.
    /// </summary>
    private static IEnumerable<string> MarkupUnder(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*.axaml"))
        {
            if (!Path.GetFileName(file).Contains(".received.", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            string name = Path.GetFileName(child);
            if (name is "bin" or "obj" || name.StartsWith('.'))
            {
                continue;
            }

            foreach (string file in MarkupUnder(child))
            {
                yield return file;
            }
        }
    }

    private static (int Code, string Output, string Error) Dotnet(params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            WorkingDirectory = RepoPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start dotnet.");

        // Both pipes drained at once: a child that fills one while the other is read to its end waits for
        // ever, and the test then hangs rather than failing.
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
