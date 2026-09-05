namespace Bennewitz.Ninja.ThemeAudit;

/// <summary>
/// One theme variant's effective resources: every key it defines after merging its shared base,
/// its variant-specific keys, and the keys it inherits from a parent variant. A key resolves to a
/// colour when it is a literal or an alias chain that ends in one; a thickness, a gradient or an
/// unresolved alias is defined but not colour-scored.
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
/// resolves to, built by <see cref="ThemeGraphWalker"/> walking the resource graph with a variant
/// context, then layering shared base keys, variant-specific keys, and inherited keys.
/// </summary>
public sealed class ThemeInventory
{
    private ThemeInventory(IReadOnlyList<VariantInventory> variants, IReadOnlyList<UnresolvedInclude> unresolved)
    {
        Variants = variants;
        Unresolved = unresolved;
    }

    /// <summary>The theme's variants, in the order they are first declared.</summary>
    public IReadOnlyList<VariantInventory> Variants { get; }

    /// <summary>Every include or code-behind reference the walker could not resolve.</summary>
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
        WalkResult walk = ThemeGraphWalker.Walk(entryFile, baseDirectory, assemblyName);

        // effectiveOwn = shared base overlaid by the variant's own keys.
        Dictionary<string, Dictionary<string, ResourceValue>> effectiveOwn = new(StringComparer.Ordinal);
        foreach (VariantId variant in walk.Variants)
        {
            Dictionary<string, ResourceValue> merged = new(walk.Base, StringComparer.Ordinal);
            foreach ((string key, ResourceValue value) in walk.Own[variant.Key])
            {
                merged[key] = value;
            }

            effectiveOwn[variant.Key] = merged;
        }

        // Inheritance by display name, transitive, child over parent; a cycle falls back to own.
        Dictionary<string, VariantId> byDisplayName = walk.Variants
            .GroupBy(v => v.DisplayName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        Dictionary<string, IReadOnlyDictionary<string, ResourceValue>> effective = new(StringComparer.Ordinal);

        IReadOnlyDictionary<string, ResourceValue> Effective(VariantId variant, HashSet<string> visiting)
        {
            if (effective.TryGetValue(variant.Key, out IReadOnlyDictionary<string, ResourceValue>? done))
            {
                return done;
            }

            Dictionary<string, ResourceValue> result;
            if (inheritance is not null
                && inheritance.TryGetValue(variant.DisplayName, out string? parentName)
                && byDisplayName.TryGetValue(parentName, out VariantId? parent)
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

        List<VariantInventory> inventories = walk.Variants
            .Select(v => new VariantInventory(v.Key, v.DisplayName, Effective(v, [])))
            .ToList();

        return new ThemeInventory(inventories, walk.Unresolved);
    }
}
