#:package Bennewitz.Ninja.XamlQuality

// scripts/xq1004-skips.cs — a .NET 10 file-based app.
//
// Re-measures the claim behind GridSlotTests' `inert-at-pin` marker: that the XamlQuality this
// repository pins never reports a grid placement as Skipped, so the gate's Skipped assertion cannot
// fail and is forward cover rather than a proven guard. The package reference carries no version on
// purpose — a file-based app takes it from Directory.Packages.props, so this measures whatever the
// repository pins today, which is the version the marker must be re-measured against when it moves.
//
// Each shape below is markup XQ1004 cannot decide from literals alone. It is scanned, never compiled,
// so it can say things the XAML compiler would reject.
//
//   exit 0  no shape was skipped: the marker's claim holds at this pin — re-mark it with this version
//   exit 1  some shape was skipped: the rule can now skip, so the guard is owed a mutation, not a marker
//
// The .sh and .ps1 wrappers beside this file run it from the repository root for you.

using Bennewitz.Ninja.XamlQuality;
using Bennewitz.Ninja.XamlQuality.Rules;

string root = Path.Combine(Path.GetTempPath(), "xq1004-skips-" + Guid.NewGuid().ToString("N"));

const string Ns = "xmlns=\"https://github.com/avaloniaui\"";
(string Name, string Markup)[] shapes =
[
    ("literal overflow, the control", $"<Grid {Ns} ColumnDefinitions=\"Auto,16,*\"><Border Grid.Column=\"1\" MinWidth=\"40\"/></Grid>"),
    ("bound shorthand definitions", $"<Grid {Ns} ColumnDefinitions=\"{{Binding Cols}}\"><Border Grid.Column=\"1\" MinWidth=\"40\"/></Grid>"),
    ("resource shorthand definitions", $"<Grid {Ns} ColumnDefinitions=\"{{StaticResource Cols}}\"><Border Grid.Column=\"1\" MinWidth=\"40\"/></Grid>"),
    ("bound element width", $"<Grid {Ns}><Grid.ColumnDefinitions><ColumnDefinition Width=\"{{Binding W}}\"/><ColumnDefinition Width=\"16\"/></Grid.ColumnDefinitions><Border Grid.Column=\"1\" MinWidth=\"40\"/></Grid>"),
    ("resource element width", $"<Grid {Ns}><Grid.ColumnDefinitions><ColumnDefinition Width=\"16\"/><ColumnDefinition Width=\"{{DynamicResource W}}\"/></Grid.ColumnDefinitions><Border Grid.Column=\"1\" MinWidth=\"40\"/></Grid>"),
    ("unparseable shorthand", $"<Grid {Ns} ColumnDefinitions=\"16,banana\"><Border Grid.Column=\"0\" MinWidth=\"40\"/></Grid>"),
    ("bound column index", $"<Grid {Ns} ColumnDefinitions=\"16,16\"><Border Grid.Column=\"{{Binding C}}\" MinWidth=\"40\"/></Grid>"),
    ("bound minimum width", $"<Grid {Ns} ColumnDefinitions=\"16,16\"><Border Grid.Column=\"0\" MinWidth=\"{{Binding M}}\"/></Grid>"),
    ("span across fixed columns", $"<Grid {Ns} ColumnDefinitions=\"16,16\"><Border Grid.Column=\"0\" Grid.ColumnSpan=\"2\" MinWidth=\"40\"/></Grid>"),
];

int skippedShapes = 0;
int controlFindings = 0;
try
{
    for (int i = 0; i < shapes.Length; i++)
    {
        string dir = Path.Combine(root, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Probe.axaml"), shapes[i].Markup);

        XamlRuleResult result = new GridSlotOverflowRule().Analyze(XamlScanContext.Load(dir));
        controlFindings = i == 0 ? result.Findings.Count : controlFindings;
        skippedShapes += result.Skipped.Count > 0 ? 1 : 0;
        Console.WriteLine($"  {shapes[i].Name,-32} inspected {result.Inspected}  findings {result.Findings.Count}  skipped {result.Skipped.Count}");
    }
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, recursive: true);
    }
}

// ⛔ The control shape must be found, or the probe has proven nothing: a rule that reads nothing also
// skips nothing, and an exit 0 from it would re-mark the guard on no evidence at all.
if (controlFindings == 0)
{
    Console.Error.WriteLine("xq1004-skips: the rule found nothing in the literal overflow — it is not reading this markup,");
    Console.Error.WriteLine("so an empty Skipped proves nothing. Nothing is re-measured.");
    return 2;
}

Console.WriteLine(skippedShapes == 0
    ? "no shape was skipped: the inert-at-pin claim holds for the pinned XamlQuality"
    : $"{skippedShapes} shape(s) skipped: the rule can skip at this pin, so the guard is owed a mutation");
return skippedShapes == 0 ? 0 : 1;
