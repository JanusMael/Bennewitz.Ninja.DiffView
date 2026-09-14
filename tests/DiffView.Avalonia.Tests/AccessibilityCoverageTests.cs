using System.Xml;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Every interactive control in the library's templates and the demo's views carries
/// <c>AutomationProperties.Name</c>, so screen readers announce it. Adapted from ClaudeForge's
/// <c>AxamlAccessibilityCoverageTests</c> (MIT): a per-file baseline that can only ratchet
/// down. Here the baseline starts at zero for every file, so any new unnamed control fails.
/// </summary>
public sealed class AccessibilityCoverageTests
{
    /// <summary>
    /// Elements that present a focusable control. ClaudeForge's set plus <c>MenuItem</c>, since
    /// menus are the demo's main interactive surface.
    /// </summary>
    private static readonly HashSet<string> InteractiveControlElements = new(StringComparer.Ordinal)
    {
        "Button",
        "ToggleButton",
        "RepeatButton",
        "TextBox",
        "ComboBox",
        "CheckBox",
        "ToggleSwitch",
        "RadioButton",
        "Slider",
        "NumericUpDown",
        "DataGrid",
        "ListBox",
        "AutoCompleteBox",
        "DatePicker",
        "TimePicker",
        "CalendarDatePicker",
        "MenuItem",
        // The editor panes take keyboard focus and carry a caret: interactive, so named; the
        // composite hosts two of them.
        "TextEditor",
        "DiffPanePresenter",
        "SideBySideDiffView",
        "InlineDiffView",
        // Clicked and dragged, so named.
        "ChangeConnectorGutter",
        "DiffMinimap",
    };

    /// <summary>
    /// Unnamed-control counts a file is allowed to keep. Empty: every file must be at zero. A file
    /// listed here with a count above zero is debt that only decreases.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> Baseline = new Dictionary<string, int>(StringComparer.Ordinal);

    private static readonly string[] ScannedDirectories = ["src/DiffView.Avalonia", "src/DiffView.Demo"];

    [Fact]
    public void Every_interactive_control_has_an_automation_name()
    {
        Dictionary<string, int> actual = new(StringComparer.Ordinal);
        foreach (string directory in ScannedDirectories.Select(RepoPaths.Source))
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*.axaml", SearchOption.AllDirectories)
                                              .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                                          && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
            {
                actual[Path.GetRelativePath(RepoPaths.Root, path)] = CountUnnamedInteractiveControls(path);
            }
        }

        Assert.NotEmpty(actual);

        List<string> failures = [];
        foreach ((string file, int count) in actual.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            int allowed = Baseline.GetValueOrDefault(file, 0);
            if (count > allowed)
            {
                failures.Add($"  {file}: {count} unnamed interactive control(s), baseline {allowed}");
            }
        }

        foreach (string file in Baseline.Keys.Where(f => !actual.ContainsKey(f)))
        {
            failures.Add($"  baseline entry '{file}' no longer exists — remove it");
        }

        if (failures.Count > 0)
        {
            Assert.Fail(
                "Accessibility coverage regression — every interactive control needs AutomationProperties.Name:"
                + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }
    }

    private static int CountUnnamedInteractiveControls(string axamlPath)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(axamlPath);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Failed to parse {axamlPath} as XML: {ex.Message}", ex);
        }

        return document.Descendants()
            .Count(element => InteractiveControlElements.Contains(element.Name.LocalName)
                              && !element.Attributes().Any(a => a.Name.LocalName == "AutomationProperties.Name"));
    }
}
