using System.Xml.Linq;
using Bennewitz.Ninja.DiffView.Tests;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 1: the project exists, the solution knows about it, and it can never be published.
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
}
