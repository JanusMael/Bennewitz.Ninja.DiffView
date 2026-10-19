using System.Text;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// A directory tree on the real disk, under the temp directory, deleted on dispose. Real rather than
/// simulated because what the corpus must get right — separators, case, hidden entries, links — is
/// the platform's, and CI runs this on three of them.
/// </summary>
internal sealed class FixtureTree : IDisposable
{
    internal FixtureTree()
    {
        Root = Path.Combine(Path.GetTempPath(), $"agent-baseline-corpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    /// <summary>The tree's root, a full path.</summary>
    internal string Root { get; }

    /// <summary>The root's own name, which a label may need to reach to be unique.</summary>
    internal string RootName => Path.GetFileName(Root);

    /// <summary>The full path of <paramref name="relative"/>, written with <c>/</c> on every platform.</summary>
    internal string PathOf(string relative) =>
        Path.GetFullPath(Path.Combine([Root, .. relative.Split('/')]));

    /// <summary>Writes <paramref name="content"/> as UTF-8 at <paramref name="relative"/>, making its directories.</summary>
    internal string Write(string relative, string content) => WriteBytes(relative, Encoding.UTF8.GetBytes(content));

    /// <summary>Writes <paramref name="bytes"/> at <paramref name="relative"/>, making its directories.</summary>
    internal string WriteBytes(string relative, byte[] bytes)
    {
        string path = PathOf(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Makes the directory <paramref name="relative"/>.</summary>
    internal string MakeDirectory(string relative)
    {
        string path = PathOf(relative);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            // A recursive delete removes a directory link without following it.
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the temp directory's own cleanup; a test's verdict is not the cleanup's.
        }
    }
}

/// <summary>
/// The real disk with every call recorded — the instrument plan 00034 step 4 asks for, since
/// <i>"no candidate came out"</i> passes for a walk that visited everything first.
/// </summary>
internal sealed class RecordingFileSystem : ICorpusFileSystem
{
    private readonly ICorpusFileSystem inner = LocalFileSystem.Instance;

    /// <summary>Every directory listed, in order: the directories the walk descended into.</summary>
    internal List<string> Listed { get; } = [];

    /// <summary>Every path any call was given, listing or not.</summary>
    internal List<string> Touched { get; } = [];

    /// <summary>Directories whose listing throws as an unreadable directory would.</summary>
    internal HashSet<string> Unreadable { get; } = new(StringComparer.Ordinal);

    /// <summary>Files whose reading throws as a locked file would.</summary>
    internal HashSet<string> UnreadableFiles { get; } = new(StringComparer.Ordinal);

    public bool FileExists(string path)
    {
        Touched.Add(path);
        return inner.FileExists(path);
    }

    public bool DirectoryExists(string path)
    {
        Touched.Add(path);
        return inner.DirectoryExists(path);
    }

    public byte[] ReadAllBytes(string path)
    {
        Touched.Add(path);
        return UnreadableFiles.Contains(path)
            ? throw new IOException($"The process cannot access the file '{path}' because it is being used by another process.")
            : inner.ReadAllBytes(path);
    }

    public DirectoryListing List(string directory)
    {
        Touched.Add(directory);
        Listed.Add(directory);
        return Unreadable.Contains(directory)
            ? throw new UnauthorizedAccessException($"Access to the path '{directory}' is denied.")
            : inner.List(directory);
    }

    /// <summary>Whether any call was given <paramref name="directory"/> or a path below it.</summary>
    internal bool TouchedAtOrUnder(string directory) =>
        Touched.Any(p => string.Equals(p, directory, Corpus.PathComparison)
                      || p.StartsWith(directory + Path.DirectorySeparatorChar, Corpus.PathComparison));
}
