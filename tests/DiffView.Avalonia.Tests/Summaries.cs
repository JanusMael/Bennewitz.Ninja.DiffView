using System.Reflection;
using System.Xml.Linq;
using Bennewitz.Ninja.DiffView.Avalonia;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests;

/// <summary>
/// The <c>&lt;summary&gt;</c> of every <see cref="DiffViewStrings"/> key, read from the XML
/// documentation build output.
/// </summary>
/// <remarks>
/// From the documentation file rather than the source, because that is what
/// <c>scripts/gen-locale-review.cs</c> reads: a summary that survives the compiler and not the doc
/// build would pass a source-reading test and still leave a reviewer's context column empty. Shared
/// so the gate and the generator cannot disagree about what a key's context is.
/// </remarks>
internal static class Summaries
{
    private const string Prefix = "F:Bennewitz.Ninja.DiffView.Avalonia.DiffViewStrings.";
    private const string DocumentationFile = "DiffView.Avalonia.xml";

    /// <summary>By constant name — <c>HeaderDirty</c>, not <c>Header.Dirty</c>.</summary>
    internal static IReadOnlyDictionary<string, string> ByField()
    {
        string path = Path.Combine(AppContext.BaseDirectory, DocumentationFile);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "The XML documentation file is missing, so every summary would read as absent and the tests over it " +
                "would report every key as undocumented instead of the truth. GenerateDocumentationFile is true in " +
                "DiffView.Avalonia.csproj; a build produces it beside the test assembly.",
                path);
        }

        Dictionary<string, string> found = new(StringComparer.Ordinal);
        foreach (XElement member in XDocument.Load(path).Descendants("member"))
        {
            string? id = member.Attribute("name")?.Value;
            XElement? summary = member.Element("summary");
            if (id is not null && summary is not null && id.StartsWith(Prefix, StringComparison.Ordinal))
            {
                // summary.Value rather than the element's own text: the placeholders sit inside <c>
                // elements, and reading only the direct text would drop every one of them.
                found[id[Prefix.Length..]] = Collapse(summary.Value);
            }
        }

        return found;
    }

    /// <summary>By the key the constant holds — <c>Header.Dirty</c>, not <c>HeaderDirty</c>.</summary>
    internal static IReadOnlyDictionary<string, string> ByKey()
    {
        IReadOnlyDictionary<string, string> byField = ByField();
        Dictionary<string, string> byKey = new(StringComparer.Ordinal);

        foreach (FieldInfo field in typeof(DiffViewStrings).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (field is { IsLiteral: true, IsInitOnly: false }
                && field.FieldType == typeof(string)
                && field.GetRawConstantValue() is string key
                && byField.TryGetValue(field.Name, out string? summary))
            {
                byKey[key] = summary;
            }
        }

        return byKey;
    }

    /// <summary>One line, single-spaced — the shape the generator writes into a table cell.</summary>
    private static string Collapse(string text)
    {
        return string.Join(' ', text.Split((char[])['\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
    }
}
