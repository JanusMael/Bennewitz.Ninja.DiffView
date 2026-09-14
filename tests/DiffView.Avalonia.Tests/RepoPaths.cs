namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Locates the repository root from the test's runtime directory, for tests that read source
/// files (templates, views, reference checkouts) rather than compiled resources.
/// </summary>
internal static class RepoPaths
{
    private const string Marker = "DiffView.slnx";

    public static string Root => FindRoot();

    public static string Source(string relative)
    {
        return Path.Combine(Root, relative);
    }

    private static string FindRoot()
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
