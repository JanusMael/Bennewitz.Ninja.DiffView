// A namespace segment that shadows the root namespace System, nested under a scope nothing else is
// inside: no qualified name anywhere in this assembly resolves through it, so everything still compiles.
// Code in this file names no System type for the same reason — here, System would find the segment.
// BNCQ1004, the compile-time half of the same rule, refuses the segment at the build, and is suppressed here
// alone: the shadow is the control, and only a compiled one can be read by the instance it controls.
#pragma warning disable BNCQ1004
namespace Bennewitz.Ninja.DiffView.Tests.ShadowControlScope.System;
#pragma warning restore BNCQ1004

/// <summary>
/// The standing control for <c>BNAQ1004</c> in <c>AssemblyQualityTests</c>: a type whose namespace
/// carries a segment shadowing a referenced root, which the adopted rule instance must report.
/// Internal, so a rule that reads public types only misses it.
/// </summary>
internal static class ShadowControl
{
}
