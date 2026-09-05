using System.Xml;
using System.Xml.Linq;

namespace Bennewitz.Ninja.ThemeAudit;

/// <summary>A variant discovered while walking a theme: its raw key and readable name.</summary>
internal sealed record VariantId(string Key, string DisplayName);

/// <summary>
/// The raw material a <see cref="ThemeInventory"/> is built from: the variants the theme declares,
/// the base keys shared by all of them, each variant's own keys, and any include or code-behind
/// reference that could not be resolved.
/// </summary>
internal sealed record WalkResult(
    IReadOnlyList<VariantId> Variants,
    IReadOnlyDictionary<string, ResourceValue> Base,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ResourceValue>> Own,
    IReadOnlyList<UnresolvedInclude> Unresolved);

/// <summary>
/// Walks a theme's resource graph carrying a variant context, so a key is attributed to the
/// variant whose <c>ThemeDictionaries</c> slot it was reached through — even when that slot is in a
/// file included lower down (Semi selects its Light/Dark palettes inside a shared
/// <c>Tokens/_index</c>, not the entry). Follows <c>ResourceInclude</c>, inline dictionaries, and
/// code-behind dictionary elements resolved by <see cref="XClassIndex"/>. A resource reached with
/// no variant context is a base key shared by every variant.
/// </summary>
internal static class ThemeGraphWalker
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] IncludeElements = ["ResourceInclude", "MergeResourceInclude"];

    public static WalkResult Walk(string entryFile, string baseDirectory, string? assemblyName)
    {
        string root = Path.GetFullPath(baseDirectory);
        string entry = Path.GetFullPath(entryFile);
        XClassIndex classIndex = XClassIndex.Build(root);

        Walker walker = new(root, assemblyName, classIndex);
        walker.WalkFile(entry, context: null);
        return walker.ToResult();
    }

    private sealed class Walker(string root, string? assemblyName, XClassIndex classIndex)
    {
        private readonly Dictionary<string, VariantId> _variants = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ResourceValue> _base = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, ResourceValue>> _own = new(StringComparer.Ordinal);
        private readonly List<UnresolvedInclude> _unresolved = [];
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);

        public void WalkFile(string file, VariantId? context)
        {
            if (!_visited.Add(file + "\0" + (context?.Key ?? string.Empty)))
            {
                return;
            }

            if (!File.Exists(file))
            {
                _unresolved.Add(new UnresolvedInclude(file, file, "file not found"));
                return;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(file);
            }
            catch (XmlException ex)
            {
                _unresolved.Add(new UnresolvedInclude(file, file, $"not well-formed XML: {ex.Message}"));
                return;
            }

            if (document.Root is { } documentRoot)
            {
                Process(documentRoot, file, context);
            }
        }

        private void Process(XElement element, string file, VariantId? context)
        {
            foreach (XElement child in element.Elements())
            {
                string localName = child.Name.LocalName;

                if (localName.EndsWith("ThemeDictionaries", StringComparison.Ordinal))
                {
                    foreach (XElement slotChild in child.Elements())
                    {
                        XAttribute? key = slotChild.Attribute(XamlNamespace + "Key");
                        if (key is null)
                        {
                            continue;
                        }

                        DescendReference(slotChild, file, Register(key.Value));
                    }
                }
                else if (localName.EndsWith("MergedDictionaries", StringComparison.Ordinal))
                {
                    foreach (XElement slotChild in child.Elements())
                    {
                        DescendReference(slotChild, file, context);
                    }
                }
                else
                {
                    if (child.Attribute(XamlNamespace + "Key") is { } keyed)
                    {
                        Record(context, keyed.Value, ThemeDefinitionScanner.ValueOf(child));
                    }

                    Process(child, file, context);
                }
            }
        }

        private void DescendReference(XElement node, string includingFile, VariantId? context)
        {
            string localName = node.Name.LocalName;

            if (IncludeElements.Contains(localName, StringComparer.Ordinal))
            {
                string? source = node.Attribute("Source")?.Value;
                if (string.IsNullOrWhiteSpace(source))
                {
                    return;
                }

                if (ResourceIncludeResolver.TryResolveSource(source.Trim(), includingFile, root, assemblyName, out string resolved, out string reason))
                {
                    WalkFile(resolved, context);
                }
                else
                {
                    _unresolved.Add(new UnresolvedInclude(includingFile, source.Trim(), reason));
                }
            }
            else if (localName == "ResourceDictionary")
            {
                Process(node, includingFile, context); // inline dictionary; its keys take this context
            }
            else
            {
                // A code-behind dictionary element (e.g. <semi:Light/>): resolve by x:Class.
                if (classIndex.TryResolve(localName, out string file, out string reason))
                {
                    WalkFile(file, context);
                }
                else
                {
                    _unresolved.Add(new UnresolvedInclude(includingFile, localName, reason));
                }
            }
        }

        private VariantId Register(string key)
        {
            if (!_variants.TryGetValue(key, out VariantId? variant))
            {
                variant = new VariantId(key, ThemeVariant.DisplayNameOf(key));
                _variants[key] = variant;
                _own[key] = new Dictionary<string, ResourceValue>(StringComparer.Ordinal);
            }

            return variant;
        }

        private void Record(VariantId? context, string key, ResourceValue value)
        {
            if (context is null)
            {
                _base[key] = value;
            }
            else
            {
                _own[context.Key][key] = value;
            }
        }

        public WalkResult ToResult()
        {
            Dictionary<string, IReadOnlyDictionary<string, ResourceValue>> own =
                _own.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<string, ResourceValue>)kv.Value, StringComparer.Ordinal);
            return new WalkResult([.. _variants.Values], _base, own, _unresolved);
        }
    }
}
