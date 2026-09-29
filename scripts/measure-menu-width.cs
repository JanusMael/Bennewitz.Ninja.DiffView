#:package Wcwidth

// scripts/measure-menu-width.cs — a .NET 10 file-based app.
//
// How wide each shipped locale's menu entries are against English, in display columns. The pane's
// context menu was narrowed once for length, and whether a translation breaks it is judged by hand at
// real size; this says what to look at first. A character count is the wrong measure for ja-JP, ko-KR
// and zh-CN: a CJK glyph takes two columns, so 前後を含めて差分表示 is 20 of them, not 10. The widths are
// Unicode's East Asian Width, from Wcwidth, because .NET carries no such table. Run from the
// repository root; the .sh and .ps1 wrappers beside this file do that for you.
//
//   dotnet run scripts/measure-menu-width.cs                        every shipped locale against English
//   dotnet run scripts/measure-menu-width.cs -- --columns <text>    one text's width, and nothing else

using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Wcwidth;

const string LocaleDirectory = "src/DiffView.Avalonia/Localization";

// The pane context menu's entries, which are the surface a longer translation breaks.
const string MenuPrefix = "Menu.";

if (args is ["--columns", var text])
{
    Console.WriteLine(Columns(text).ToString(CultureInfo.InvariantCulture));
    return 0;
}

if (args.Length != 0)
{
    Console.Error.WriteLine("usage: measure-menu-width [--columns <text>]");
    return 2;
}

string neutral = Path.Combine(LocaleDirectory, "Strings.resx");
if (!File.Exists(neutral))
{
    Console.Error.WriteLine($"measure-menu-width: there is no {neutral} here; run from the repository root, as the wrappers do");
    return 2;
}

Dictionary<string, string> english = Read(neutral);
string[] menuKeys = [.. english.Keys.Where(k => k.StartsWith(MenuPrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
string widestEnglishKey = menuKeys.MaxBy(k => Columns(english[k]))!;
int widestEnglish = Columns(english[widestEnglishKey]);

// Every shipped locale is a Strings.<culture>.resx beside the neutral one.
string[] cultures =
[
    .. Directory.GetFiles(LocaleDirectory, "Strings.*.resx")
        .Select(path => Path.GetFileName(path)["Strings.".Length..^".resx".Length])
        .Order(StringComparer.Ordinal),
];

Console.WriteLine(Invariant($"English: {menuKeys.Length} menu entries, widest {widestEnglish} columns ({widestEnglishKey})"));
Console.WriteLine();
Console.WriteLine(Invariant($"{"locale",-8} {"widest",7} {"vs en",7}  {">en max",8}  widest entry"));
Console.WriteLine(new string('-', 96));

List<(string Culture, Dictionary<string, string> Values, (string Key, int Width)[] Over)> wider = [];
foreach (string culture in cultures)
{
    Dictionary<string, string> values = Read(Path.Combine(LocaleDirectory, $"Strings.{culture}.resx"));
    string widestKey = menuKeys.MaxBy(k => Columns(values[k]))!;
    int widest = Columns(values[widestKey]);
    (string Key, int Width)[] over =
    [
        .. menuKeys.Select(k => (Key: k, Width: Columns(values[k])))
            .Where(entry => entry.Width > widestEnglish)
            .OrderByDescending(entry => entry.Width),
    ];
    wider.Add((culture, values, over));
    string ratio = (widest / (double)widestEnglish).ToString("0%", CultureInfo.InvariantCulture);
    Console.WriteLine(Invariant($"{culture,-8} {widest,7} {ratio,7}  {over.Length,8}  {values[widestKey]}"));
}

Console.WriteLine();
Console.WriteLine("Entries wider than the widest English one, per locale:");
foreach ((string culture, Dictionary<string, string> values, (string Key, int Width)[] over) in wider)
{
    if (over.Length == 0)
    {
        Console.WriteLine($"  {culture}: none");
        continue;
    }

    Console.WriteLine($"  {culture}:");
    foreach ((string key, int width) in over)
    {
        Console.WriteLine(Invariant($"     {width,3}  {values[key]}"));
        Console.WriteLine(Invariant($"          (en {Columns(english[key]),3}  {english[key]})"));
    }
}

return 0;

// East Asian Wide and Fullwidth characters take two columns and the rest one, as the Python this
// replaces measured them; a combining mark takes none. An ambiguous-width character — the ellipsis
// every locale's menu carries — takes one, as a terminal outside East Asia draws it.
static int Columns(string text)
{
    int columns = 0;
    foreach (Rune rune in text.EnumerateRunes())
    {
        columns += Math.Max(0, UnicodeCalculator.GetWidth(rune));
    }

    return columns;
}

static Dictionary<string, string> Read(string path) =>
    XDocument.Load(path).Root!.Elements("data").ToDictionary(
        data => (string)data.Attribute("name")!,
        data => (string?)data.Element("value") ?? string.Empty,
        StringComparer.Ordinal);

static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
