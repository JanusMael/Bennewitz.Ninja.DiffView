namespace Bennewitz.Ninja.ThemeAudit.Tests;

/// <summary>Locates the committed fixtures under this test project, and the repository root, from the runtime directory.</summary>
internal static class FixturePaths
{
    private const string ProjectMarker = "ThemeAudit.Tests.csproj";
    private const string RepoMarker = "DiffView.slnx";

    /// <summary>The directory of the named fixture under <c>Fixtures/</c>.</summary>
    public static string Fixture(string name)
    {
        return Path.Combine(FindUp(ProjectMarker), "Fixtures", name);
    }

    /// <summary>The repository root — where <c>theme-audit.json</c> and <c>docs/theme-audit.md</c> live.</summary>
    public static string RepoRoot => FindUp(RepoMarker);

    private static string FindUp(string marker)
    {
        string? directory = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && !string.IsNullOrEmpty(directory); i++)
        {
            if (File.Exists(Path.Combine(directory, marker)))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            $"Could not find {marker} by walking up from AppContext.BaseDirectory = '{AppContext.BaseDirectory}'.");
    }
}
