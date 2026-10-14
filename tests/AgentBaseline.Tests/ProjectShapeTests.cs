using System.Xml.Linq;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 1: the project exists, the solution knows about it, it can never be published,
/// and it carries no markup yet.
/// </summary>
/// <remarks>
/// ⛔ These are not ceremony. Step 1's own verification says *"a project absent from the solution is
/// built on no runner, and draft 1's verification would have passed in that state"* — a project that
/// compiles locally while no CI leg ever sees it is precisely the state these assertions refuse.
/// </remarks>
public sealed class ProjectShapeTests
{
    private const string Solution = "DiffView.slnx";
    private const string AppProject = "src/AgentBaseline/AgentBaseline.csproj";
    private const string TestProject = "tests/AgentBaseline.Tests/AgentBaseline.Tests.csproj";

    private static IReadOnlyList<string> SolutionProjects()
    {
        XDocument document = XDocument.Load(Path.Combine(RepoPaths.Root, Solution));
        return document.Descendants("Project")
            .Select(e => (string?)e.Attribute("Path"))
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!.Replace('\\', '/'))
            .ToList();
    }

    [Fact]
    public void The_solution_builds_this_project_so_some_runner_does()
    {
        IReadOnlyList<string> projects = SolutionProjects();

        Assert.Contains(AppProject, projects);
        Assert.Contains(TestProject, projects);
    }

    [Fact]
    public void The_app_can_never_be_published()
    {
        XDocument document = XDocument.Load(Path.Combine(RepoPaths.Root, AppProject));
        string[] packable = document.Descendants("PropertyGroup")
            .Elements("IsPackable")
            .Select(e => e.Value.Trim())
            .ToArray();

        // Asserted as a declared false rather than as "not true": PackagingTests reads a project as
        // packable only when IsPackable says true, so an absent property passes that gate while
        // saying nothing. The release names the packages it pushes, and this project is not one.
        Assert.Equal(["false"], packable);
    }

    [Fact]
    public void No_markup_lives_here_yet_and_the_first_file_that_does_owes_a_gate()
    {
        string root = Path.Combine(RepoPaths.Root, "src", "AgentBaseline");
        string[] markup = Directory.Exists(root)
            ? Directory.GetFiles(root, "*.axaml", SearchOption.AllDirectories)
                .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(p => Path.GetRelativePath(root, p))
                .ToArray()
            : [];

        // ⭐ A tripwire with its instructions attached, and it is MEANT to fire at plan 00034 step 5.
        // AccessibilityCoverageTests asserts library + demo == its whole scan of src, so the first
        // .axaml here makes that equality false and the suite goes red for a reason that names
        // neither this project nor this plan. Deleting this test is the wrong fix.
        Assert.True(
            markup.Length == 0,
            $"Markup has appeared under src/AgentBaseline ({string.Join(", ", markup)}), so this "
            + "project is now the THIRD markup-bearing project under src. AccessibilityCoverageTests "
            + "accounts for every element its scan of src finds against the library's and the demo's "
            + "two subjects, and that equality is now false. Pay plan 00034 step 11 in this same "
            + "change — this project's own subject and its own measured floor, AutomationProperties."
            + "Name on every interactive control, BNXQ1004 clean — and then delete this test, which "
            + "exists only to say so.");
    }
}
