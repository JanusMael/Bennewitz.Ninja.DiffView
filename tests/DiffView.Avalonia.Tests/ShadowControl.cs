// A namespace segment that shadows the root namespace System, nested under a scope nothing else is
// inside: no qualified name anywhere in this assembly resolves through it, so everything still compiles.
// Code in this file names no System type for the same reason — here, System would find the segment.
namespace Bennewitz.Ninja.DiffView.Tests.ShadowControlScope.System;

/// <summary>
/// The standing control for <c>BNAQ1004</c> in <c>AssemblyQualityTests</c>: a type whose namespace
/// carries a segment shadowing a referenced root, which the adopted rule instance must report.
/// Internal, so a rule that reads public types only misses it.
/// </summary>
internal static class ShadowControl
{
}
