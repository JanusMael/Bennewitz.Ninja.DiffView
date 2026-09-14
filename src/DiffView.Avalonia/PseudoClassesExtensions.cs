using Avalonia.Controls;

namespace Bennewitz.Ninja.DiffView;

/// <summary>Adds or removes a pseudo-class in one call, so a state maps to a class without branching at every site.</summary>
internal static class PseudoClassesExtensions
{
    public static void Set(this IPseudoClasses classes, string name, bool present)
    {
        if (present)
        {
            classes.Add(name);
        }
        else
        {
            classes.Remove(name);
        }
    }
}
