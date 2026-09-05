namespace Bennewitz.Ninja.ThemeAudit;

/// <summary>The AXAML/XAML files under a directory, in ordinal path order, skipping build output and git.</summary>
internal static class XamlFiles
{
    private static readonly string[] SkippedDirectories = ["bin", "obj", ".git"];

    public static IReadOnlyList<string> Enumerate(string directory)
    {
        return Directory
            .EnumerateFiles(directory, "*.*xaml", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !IsUnderSkippedDirectory(directory, f))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsUnderSkippedDirectory(string root, string file)
    {
        string relative = Path.GetRelativePath(root, file);
        string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Take(segments.Length - 1)
                       .Any(segment => SkippedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }
}
