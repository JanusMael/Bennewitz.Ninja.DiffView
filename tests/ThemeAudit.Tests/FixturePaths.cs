namespace Bennewitz.Ninja.ThemeAudit.Tests;

/// <summary>Locates the committed fixtures under this test project from the runtime directory.</summary>
internal static class FixturePaths
{
    private const string Marker = "ThemeAudit.Tests.csproj";

    /// <summary>The directory of the named fixture under <c>Fixtures/</c>.</summary>
    public static string Fixture(string name)
    {
        return Path.Combine(ProjectRoot(), "Fixtures", name);
    }

    private static string ProjectRoot()
    {
        string? directory = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && !string.IsNullOrEmpty(directory); i++)
        {
            if (File.Exists(Path.Combine(directory, Marker)))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            $"Could not find {Marker} by walking up from AppContext.BaseDirectory = '{AppContext.BaseDirectory}'.");
    }
}
