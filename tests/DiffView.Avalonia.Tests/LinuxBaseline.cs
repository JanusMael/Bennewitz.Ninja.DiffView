using Xunit.v3;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Marks a test whose evidence is a committed image baseline, so the runner leaves it out on every
/// platform but Linux — the one its baselines were rendered and reviewed on.
/// </summary>
/// <remarks>
/// <para>
/// ⛔ <b>A baseline is a record of what a human accepted, and it was accepted on Linux.</b>
/// Rasterization is the platform's: the bundled font fixes <em>which</em> glyphs are drawn and
/// nothing about <em>how</em>, so the same frame differs by 10–13% of its pixels on Windows and on
/// macOS without anything being wrong. A tolerance wide enough to pass that would hide a real
/// regression, and a second and third set of baselines would be verifications nobody performed.
/// </para>
/// <para>
/// ⭐ <b>The gate is a trait the runner filters, never a skip inside the test.</b>
/// <c>tests/Directory.Build.props</c> excludes <c>Baseline=Linux</c> away from Linux, and CI says how
/// many it left out, because a filter is silent in the test summary: thirty tests that quietly vanish
/// read as thirty that passed. <c>-p:ExcludeLinuxBaselines=false</c> runs them anywhere.
/// </para>
/// <para>
/// On a class when every test in it compares a frame; on a method where the class also holds a test
/// that does not, as <c>SyntaxSnapshotTests</c> does. A pixel assertion measured beside a capture is a
/// property, not a picture, and runs everywhere.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class LinuxBaselineAttribute : Attribute, ITraitAttribute
{
    /// <summary>The trait's name, as the runner's filter spells it.</summary>
    public const string TraitName = "Baseline";

    /// <summary>The trait's value, as the runner's filter spells it.</summary>
    public const string TraitValue = "Linux";

    /// <inheritdoc/>
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new(TraitName, TraitValue)];
}
