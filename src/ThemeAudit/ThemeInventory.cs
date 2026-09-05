namespace Bennewitz.Ninja.ThemeAudit;

/// <summary>
/// One theme variant's effective resources: every key it defines after merging its shared base,
/// its variant-specific keys, and — through <see cref="ThemeInventory"/> — the keys it inherits
/// from a parent variant. A key resolves to a colour when it is a literal or an alias chain that
/// ends in one; a thickness, a gradient or an unresolved alias is defined but not colour-scored.
/// </summary>
public sealed class VariantInventory
{
    private readonly IReadOnlyDictionary<string, ResourceValue> _effective;

    internal VariantInventory(string key, string displayName, IReadOnlyDictionary<string, ResourceValue> effective)
    {
        Key = key;
        DisplayName = displayName;
        _effective = effective;
    }

    /// <summary>The raw variant key (e.g. <c>Dark</c> or <c>{x:Static semi:SemiTheme.NightSky}</c>).</summary>
    public string Key { get; }

    /// <summary>The readable variant name (e.g. <c>NightSky</c>).</summary>
    public string DisplayName { get; }

    /// <summary>Every key this variant defines, after base merge and inheritance.</summary>
    public IReadOnlyCollection<string> Keys => (IReadOnlyCollection<string>)_effective.Keys;

    /// <summary>Whether this variant defines <paramref name="key"/> at all.</summary>
    public bool Defines(string key) => _effective.ContainsKey(key);

    /// <summary>
    /// The colour <paramref name="key"/> resolves to under this variant, following an alias chain
    /// to its literal; <c>null</c> when the key is undefined, opaque, or an alias that dangles or
    /// cycles.
    /// </summary>
    public AuditColor? Resolve(string key)
    {
        return Resolve(key, []);
    }

    private AuditColor? Resolve(string key, HashSet<string> visiting)
    {
        if (!_effective.TryGetValue(key, out ResourceValue? value) || !visiting.Add(key))
        {
            return null;
        }

        return value switch
        {
            ResourceValue.ColorLiteral literal => literal.Color,
            ResourceValue.Alias alias => Resolve(alias.TargetKey, visiting),
            _ => null,
        };
    }
}

/// <summary>
/// The per-variant inventory of a theme: which keys each variant defines and what colour each
/// resolves to, built by walking the variant map's include graph, scanning every contributing
/// file, and layering shared base keys, variant-specific keys, and inherited keys.
/// </summary>
public sealed class ThemeInventory
{
    private ThemeInventory(IReadOnlyList<VariantInventory> variants, IReadOnlyList<UnresolvedInclude> unresolved)
    {
        Variants = variants;
        Unresolved = unresolved;
    }

    /// <summary>The theme's variants, in declaration order.</summary>
    public IReadOnlyList<VariantInventory> Variants { get; }

    /// <summary>Every include the resolver could not turn into a local file.</summary>
    public IReadOnlyList<UnresolvedInclude> Unresolved { get; }

    /// <summary>
    /// Builds the inventory for the theme rooted at <paramref name="entryFile"/>.
    /// <paramref name="baseDirectory"/> is the assembly's <c>avares</c> base and
    /// <paramref name="assemblyName"/> its assembly (so its own <c>avares://</c> includes resolve).
    /// <paramref name="inheritance"/> maps a variant's display name to the display name of the
    /// variant it inherits from — Semi's Desert inherits Light, its Aquatic/Dusk/NightSky inherit
    /// Dark — so a key defined only on the parent still counts as defined on the child.
    /// </summary>
    public static ThemeInventory Build(
        string entryFile,
        string baseDirectory,
        string? assemblyName = null,
        IReadOnlyDictionary<string, string>? inheritance = null)
    {
        string entry = Path.GetFullPath(entryFile);
        ThemeVariantMap map = ThemeVariantMapParser.Parse(entry, baseDirectory, assemblyName);
        List<UnresolvedInclude> unresolved = [.. map.Unresolved];

        // Shared base keys (Variant == null) apply to every variant; a variant key found while
        // scanning a shared file (an inline-variant entry) routes to that variant instead.
        Dictionary<string, ResourceValue> sharedBase = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, ResourceValue>> own = new(StringComparer.Ordinal);
        foreach (ThemeVariant variant in map.Variants)
        {
            own[variant.Key] = new Dictionary<string, ResourceValue>(StringComparer.Ordinal);
        }

        Dictionary<string, ResourceValue> BucketFor(string? variantKey)
        {
            if (variantKey is null)
            {
                return sharedBase;
            }

            if (!own.TryGetValue(variantKey, out Dictionary<string, ResourceValue>? bucket))
            {
                bucket = new Dictionary<string, ResourceValue>(StringComparer.Ordinal);
                own[variantKey] = bucket;
            }

            return bucket;
        }

        // The entry file is scanned directly (not expanded), because expanding it would follow its
        // ThemeDictionaries includes and pull variant files into the shared set. Its own base keys
        // are shared; its inline-variant keys route to their variant.
        foreach (DefinedResource resource in ThemeDefinitionScanner.ScanFile(entry))
        {
            BucketFor(resource.Variant)[resource.Key] = resource.Value;
        }

        // Other shared roots (top-level merged includes) are expanded and contribute base keys.
        foreach (string sharedRoot in map.SharedRoots.Where(r => !string.Equals(r, entry, StringComparison.Ordinal)))
        {
            IncludeResolution resolution = ResourceIncludeResolver.Resolve(sharedRoot, baseDirectory, assemblyName);
            unresolved.AddRange(resolution.Unresolved);
            foreach (string file in resolution.Files)
            {
                foreach (DefinedResource resource in ThemeDefinitionScanner.ScanFile(file))
                {
                    BucketFor(resource.Variant)[resource.Key] = resource.Value;
                }
            }
        }

        // Each variant's own include roots contribute variant-specific keys.
        foreach (ThemeVariant variant in map.Variants)
        {
            foreach (string variantRoot in variant.Roots)
            {
                IncludeResolution resolution = ResourceIncludeResolver.Resolve(variantRoot, baseDirectory, assemblyName);
                unresolved.AddRange(resolution.Unresolved);
                foreach (string file in resolution.Files)
                {
                    foreach (DefinedResource resource in ThemeDefinitionScanner.ScanFile(file))
                    {
                        BucketFor(resource.Variant ?? variant.Key)[resource.Key] = resource.Value;
                    }
                }
            }
        }

        // effectiveOwn = sharedBase overlaid by the variant's own keys.
        Dictionary<string, Dictionary<string, ResourceValue>> effectiveOwn = new(StringComparer.Ordinal);
        foreach (ThemeVariant variant in map.Variants)
        {
            Dictionary<string, ResourceValue> merged = new(sharedBase, StringComparer.Ordinal);
            foreach ((string key, ResourceValue value) in own[variant.Key])
            {
                merged[key] = value;
            }

            effectiveOwn[variant.Key] = merged;
        }

        // Apply inheritance by display name, transitively, so a child sees its parent's keys under
        // its own. Cycles fall back to the child's own effective map.
        Dictionary<string, ThemeVariant> byDisplayName = map.Variants
            .GroupBy(v => v.DisplayName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        Dictionary<string, IReadOnlyDictionary<string, ResourceValue>> effective = new(StringComparer.Ordinal);

        IReadOnlyDictionary<string, ResourceValue> Effective(ThemeVariant variant, HashSet<string> visiting)
        {
            if (effective.TryGetValue(variant.Key, out IReadOnlyDictionary<string, ResourceValue>? done))
            {
                return done;
            }

            Dictionary<string, ResourceValue> result;
            if (inheritance is not null
                && inheritance.TryGetValue(variant.DisplayName, out string? parentName)
                && byDisplayName.TryGetValue(parentName, out ThemeVariant? parent)
                && !ReferenceEquals(parent, variant)
                && visiting.Add(variant.Key))
            {
                result = new Dictionary<string, ResourceValue>(Effective(parent, visiting), StringComparer.Ordinal);
                foreach ((string key, ResourceValue value) in effectiveOwn[variant.Key])
                {
                    result[key] = value;
                }
            }
            else
            {
                result = new Dictionary<string, ResourceValue>(effectiveOwn[variant.Key], StringComparer.Ordinal);
            }

            effective[variant.Key] = result;
            return result;
        }

        List<VariantInventory> inventories = map.Variants
            .Select(v => new VariantInventory(v.Key, v.DisplayName, Effective(v, [])))
            .ToList();

        return new ThemeInventory(inventories, unresolved);
    }
}
