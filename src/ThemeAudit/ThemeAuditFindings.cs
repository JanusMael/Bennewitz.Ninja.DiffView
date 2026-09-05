namespace Bennewitz.Ninja.ThemeAudit;

/// <summary>
/// A key a consumer references that a theme variant does not define — the invisible-control case.
/// <see cref="WorstKind"/> is <see cref="ReferenceKind.Static"/> when any reference to the key is
/// static (it throws at load) and <see cref="ReferenceKind.Dynamic"/> otherwise (it resolves to
/// nothing and paints an invisible control).
/// </summary>
public sealed record UndefinedKeyFinding(string Variant, string DisplayName, string Key, ReferenceKind WorstKind);

/// <summary>Compares what a consumer references against what a theme defines, per variant.</summary>
public static class ThemeAuditFindings
{
    /// <summary>
    /// The keys <paramref name="references"/> use that no variant of <paramref name="inventory"/>
    /// defines and that <paramref name="alsoDefined"/> (the consumer's own resources, and any base
    /// keys the host supplies) does not cover. One finding per (variant, key), ordered by variant
    /// then key.
    /// </summary>
    public static IReadOnlyList<UndefinedKeyFinding> UndefinedKeys(
        IReadOnlyList<ResourceReference> references,
        ThemeInventory inventory,
        IReadOnlySet<string>? alsoDefined = null)
    {
        // Worst reference kind per key: Static (throws) outranks Dynamic (invisible).
        Dictionary<string, ReferenceKind> worstKind = new(StringComparer.Ordinal);
        foreach (ResourceReference reference in references)
        {
            if (!worstKind.ContainsKey(reference.Key))
            {
                worstKind[reference.Key] = reference.Kind;
            }
            else if (reference.Kind == ReferenceKind.Static)
            {
                worstKind[reference.Key] = ReferenceKind.Static;
            }
        }

        List<UndefinedKeyFinding> findings = [];
        foreach (VariantInventory variant in inventory.Variants)
        {
            foreach ((string key, ReferenceKind kind) in worstKind)
            {
                if (!variant.Defines(key) && (alsoDefined is null || !alsoDefined.Contains(key)))
                {
                    findings.Add(new UndefinedKeyFinding(variant.Key, variant.DisplayName, key, kind));
                }
            }
        }

        return findings
            .OrderBy(f => f.DisplayName, StringComparer.Ordinal)
            .ThenBy(f => f.Key, StringComparer.Ordinal)
            .ToList();
    }
}
