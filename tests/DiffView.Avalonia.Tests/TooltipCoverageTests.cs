using System.Xml;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// Every button and every other input control the repository writes in markup carries a tooltip: Brian's rule,
/// held here since 2026-11-10, when the review window was found with seven buttons and not one tooltip. Read from
/// the markup under <c>src</c>, each project's build output pruned as the walk descends rather than visited.
/// </summary>
/// <remarks>
/// A tooltip counts where markup gives it, as the attribute or as the property element. A control built in code
/// is not markup and is not read here, and a tooltip a style setter gave would be read as missing, which no
/// markup of this repository does.
/// </remarks>
public sealed class TooltipCoverageTests
{
    /// <summary>The controls a person presses, ticks, picks from or types into.</summary>
    private static readonly HashSet<string> InputControls = new(StringComparer.Ordinal)
    {
        "AutoCompleteBox", "Button", "CalendarDatePicker", "CheckBox", "ComboBox", "DatePicker", "DropDownButton",
        "NumericUpDown", "RadioButton", "RepeatButton", "Slider", "SplitButton", "TextBox", "TimePicker",
        "ToggleButton", "ToggleSplitButton", "ToggleSwitch",
    };

    /// <summary>Build output, which holds copies of the markup and is never descended.</summary>
    private static readonly HashSet<string> Pruned = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj" };

    private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Every_input_control_the_repository_writes_in_markup_carries_a_tooltip()
    {
        string src = RepoPaths.Source("src");
        Dictionary<string, int> read = new(StringComparer.Ordinal);
        List<string> missing = [];
        foreach (string file in MarkupUnder(src))
        {
            string relative = Path.GetRelativePath(src, file);
            string project = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            IEnumerable<XElement> controls = XDocument.Load(file, LoadOptions.SetLineInfo)
                .Descendants()
                .Where(e => InputControls.Contains(e.Name.LocalName));
            foreach (XElement control in controls)
            {
                read[project] = read.GetValueOrDefault(project) + 1;
                bool tooltip = control.Attributes().Any(a => a.Name.LocalName == "ToolTip.Tip")
                    || control.Elements().Any(e => e.Name.LocalName == "ToolTip.Tip");
                if (!tooltip)
                {
                    string name = control.Attribute(XName.Get("Name", Xaml))?.Value ?? control.Attribute("Name")?.Value ?? "with no name";
                    missing.Add($"{relative}:{((IXmlLineInfo)control).LineNumber} {control.Name.LocalName} {name}");
                }
            }
        }

        // ⭐ A reading that finds nothing missing because it read nothing is no reading: the library's markup and
        // the review window's each hold input controls, and each is held to having been read.
        int library = read.GetValueOrDefault("DiffView.Avalonia");
        int baseline = read.GetValueOrDefault("AgentBaseline");
        Assert.True(library >= 15, $"The scan read {library} input controls in DiffView.Avalonia's markup, which holds 15: it is not reading what it should.");
        Assert.True(baseline >= 7, $"The scan read {baseline} input controls in AgentBaseline's markup, which holds 7: it is not reading what it should.");
        Assert.True(missing.Count == 0, $"Input controls with no tooltip:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    /// <summary>Every <c>.axaml</c> under <paramref name="directory"/>, build output pruned as the walk descends.</summary>
    private static IEnumerable<string> MarkupUnder(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*.axaml"))
        {
            if (!file.EndsWith(".received.axaml", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (!Pruned.Contains(Path.GetFileName(child)))
            {
                foreach (string file in MarkupUnder(child))
                {
                    yield return file;
                }
            }
        }
    }
}
