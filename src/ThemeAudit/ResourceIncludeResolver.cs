using System.Xml;
using System.Xml.Linq;

namespace Bennewitz.Ninja.ThemeAudit;

/// <summary>A <c>Source</c> the resolver could not turn into a local file, and why.</summary>
public sealed record UnresolvedInclude(string SourceFile, string Source, string Reason);

/// <summary>The transitive closure of one entry file's resource includes.</summary>
public sealed record IncludeResolution(IReadOnlyList<string> Files, IReadOnlyList<UnresolvedInclude> Unresolved);

/// <summary>
/// Follows the <c>ResourceInclude</c> / <c>MergeResourceInclude</c> graph a theme entry file
/// builds, because a real theme (Semi) does not define its keys inline — each variant maps to an
/// included file, and those files include more. Resolution mirrors Avalonia's <c>Source</c> rules:
/// a leading <c>/</c> is rooted at the assembly's <c>avares</c> base (the project directory here),
/// a bare path is relative to the including file, and <c>avares://Assembly/path</c> is rooted when
/// the assembly is this theme's and recorded as cross-assembly otherwise. Cycles are followed once.
/// </summary>
public static class ResourceIncludeResolver
{
    private static readonly string[] IncludeElements = ["ResourceInclude", "MergeResourceInclude"];

    /// <summary>
    /// Resolves every file reachable from <paramref name="entryFile"/> through resource includes,
    /// depth-first and deduplicated, starting with the entry file itself. <paramref name="baseDirectory"/>
    /// is the assembly's <c>avares</c> root (a <c>/</c>-rooted <c>Source</c> resolves under it);
    /// <paramref name="assemblyName"/>, when given, is this theme's assembly, so an
    /// <c>avares://Assembly/…</c> source pointing at it resolves rather than being called cross-assembly.
    /// </summary>
    public static IncludeResolution Resolve(string entryFile, string baseDirectory, string? assemblyName = null)
    {
        string root = Path.GetFullPath(baseDirectory);
        List<string> ordered = [];
        List<UnresolvedInclude> unresolved = [];
        HashSet<string> visited = new(StringComparer.Ordinal);

        Visit(Path.GetFullPath(entryFile), root, assemblyName, ordered, unresolved, visited);
        return new IncludeResolution(ordered, unresolved);
    }

    private static void Visit(string file, string root, string? assemblyName,
                              List<string> ordered, List<UnresolvedInclude> unresolved, HashSet<string> visited)
    {
        if (!visited.Add(file))
        {
            return;
        }

        if (!File.Exists(file))
        {
            unresolved.Add(new UnresolvedInclude(file, file, "file not found"));
            return;
        }

        ordered.Add(file);

        XDocument document;
        try
        {
            document = XDocument.Load(file);
        }
        catch (XmlException ex)
        {
            unresolved.Add(new UnresolvedInclude(file, file, $"not well-formed XML: {ex.Message}"));
            return;
        }

        foreach (XElement element in document.Descendants())
        {
            if (!IncludeElements.Contains(element.Name.LocalName, StringComparer.Ordinal))
            {
                continue;
            }

            string? source = element.Attribute("Source")?.Value;
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            if (TryResolveSource(source.Trim(), file, root, assemblyName, out string resolved, out string reason))
            {
                Visit(resolved, root, assemblyName, ordered, unresolved, visited);
            }
            else
            {
                unresolved.Add(new UnresolvedInclude(file, source.Trim(), reason));
            }
        }
    }

    private static bool TryResolveSource(string source, string includingFile, string root, string? assemblyName,
                                         out string resolved, out string reason)
    {
        resolved = string.Empty;
        reason = string.Empty;

        string relativePath;
        if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            string rest = source["avares://".Length..];
            int slash = rest.IndexOf('/');
            if (slash < 0)
            {
                reason = "malformed avares URI";
                return false;
            }

            string assembly = rest[..slash];

            // Resolve an avares URI only when the caller declared this theme's assembly and it
            // matches; otherwise it points into another assembly we do not have on disk here.
            if (assemblyName is null || !string.Equals(assembly, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"cross-assembly: {assembly}";
                return false;
            }

            relativePath = rest[(slash + 1)..];
        }
        else if (source.StartsWith('/'))
        {
            relativePath = source[1..];
        }
        else
        {
            string directory = Path.GetDirectoryName(includingFile) ?? root;
            string candidate = Path.GetFullPath(Path.Combine(directory, source));
            return Found(candidate, out resolved, out reason);
        }

        return Found(Path.GetFullPath(Path.Combine(root, relativePath)), out resolved, out reason);
    }

    private static bool Found(string candidate, out string resolved, out string reason)
    {
        if (File.Exists(candidate))
        {
            resolved = candidate;
            reason = string.Empty;
            return true;
        }

        resolved = string.Empty;
        reason = "file not found";
        return false;
    }
}
