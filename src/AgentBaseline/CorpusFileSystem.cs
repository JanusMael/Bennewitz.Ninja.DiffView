namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// One directory's entries as the corpus walk sees them. Never recursive: what lies below a
/// subdirectory is learned only by listing that subdirectory, which is what makes a descent
/// observable.
/// </summary>
/// <param name="Directories">The subdirectories the walk may enter, in ordinal order.</param>
/// <param name="Links">
/// The subdirectories that are links — a symbolic link or a junction — which the walk never
/// follows: a tree without links is acyclic, and one with them need not be.
/// </param>
/// <param name="Files">The files, in ordinal order.</param>
internal sealed record DirectoryListing(
    IReadOnlyList<string> Directories,
    IReadOnlyList<string> Links,
    IReadOnlyList<string> Files);

/// <summary>
/// Everything the corpus reads, behind one seam: plan 00034 decision 7's transport interface, with
/// the local disk as the only thing behind it.
/// </summary>
/// <remarks>
/// <para>
/// It has two jobs. Decision 7 puts transport behind an interface and decision 14 leaves nothing
/// behind that interface but the local disk, so a provider that fetches a candidate from another
/// machine implements this and nothing else changes.
/// </para>
/// <para>
/// ⛔ And step 4's verification instruments the traversal through it. <i>"No candidate came out"</i>
/// passes for a walk that visited everything first — which is how the cost defect in decision 15's
/// first wording survived its own repair — so the claim that build output is never descended is
/// asserted as a claim about which directories were <see cref="List"/>ed, never about what the walk
/// returned.
/// </para>
/// </remarks>
internal interface ICorpusFileSystem
{
    /// <summary>Whether a file exists at <paramref name="path"/>.</summary>
    bool FileExists(string path);

    /// <summary>Whether a directory exists at <paramref name="path"/>.</summary>
    bool DirectoryExists(string path);

    /// <summary>The bytes of the file at <paramref name="path"/>.</summary>
    byte[] ReadAllBytes(string path);

    /// <summary>
    /// The entries of one directory. Throws <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> when it cannot be read, which the walk reports
    /// rather than stops on.
    /// </summary>
    DirectoryListing List(string directory);
}

/// <summary>The local disk.</summary>
internal sealed class LocalFileSystem : ICorpusFileSystem
{
    /// <summary>
    /// Every entry, hidden ones included. ⚠ <c>new EnumerationOptions()</c> skips hidden and system
    /// entries by default, and Git marks <c>.git</c> hidden on Windows — so the default would leave
    /// a pruned directory out of the count on one platform and in it on the others.
    /// </summary>
    private static readonly EnumerationOptions EveryEntry = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    private LocalFileSystem()
    {
    }

    /// <summary>The one instance; it holds no state.</summary>
    internal static LocalFileSystem Instance { get; } = new();

    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    /// <inheritdoc/>
    public DirectoryListing List(string directory)
    {
        List<string> directories = [];
        List<string> links = [];
        List<string> files = [];

        // Materialised here, so a directory that cannot be read throws from this call and not from
        // some later enumeration the walk cannot attribute.
        foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", EveryEntry))
        {
            if (entry is not DirectoryInfo subdirectory)
            {
                files.Add(entry.FullName);
            }
            else if (IsLink(subdirectory))
            {
                links.Add(subdirectory.FullName);
            }
            else
            {
                directories.Add(subdirectory.FullName);
            }
        }

        directories.Sort(StringComparer.Ordinal);
        links.Sort(StringComparer.Ordinal);
        files.Sort(StringComparer.Ordinal);
        return new DirectoryListing(directories, links, files);
    }

    /// <summary>
    /// A symbolic link or a junction. The attribute is read from the enumeration and costs nothing;
    /// the target is asked only when the attribute is set, because a reparse point is not always a
    /// link — a cloud-storage placeholder is one too, and is an ordinary directory to walk.
    /// </summary>
    private static bool IsLink(DirectoryInfo directory) =>
        (directory.Attributes & FileAttributes.ReparsePoint) != 0 && directory.LinkTarget is not null;
}
