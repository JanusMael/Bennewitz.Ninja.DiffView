using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace Bennewitz.Ninja.DiffView.Tests;

/// <summary>
/// <c>scripts/measure-menu-width</c> tells a by-hand pass in a locale which menu entries to look at
/// first. Its one judgement is the width of a string, and the wrong measure — characters rather than
/// display columns — halves every CJK entry and reports ja-JP, ko-KR and zh-CN as comfortably narrow
/// when they are not. So the tests pin that measure, and that the report applies it to every locale the
/// library ships.
/// </summary>
/// <remarks>
/// Driven through its real command line, as <see cref="CatchCrashTests"/> drives <c>catch-crash</c>.
/// </remarks>
public sealed class MeasureMenuWidthTests
{
    [Theory]
    [InlineData("前後を含めて差分表示", 20)] // the example the Python this replaces documented
    [InlineData("변경 내용", 9)]
    [InlineData("Show differences", 16)]
    [InlineData("…", 1)] // East Asian Ambiguous: one column, as a terminal outside East Asia draws it
    public void A_CJK_glyph_takes_two_columns_and_the_rest_one(string text, int columns)
    {
        (int exitCode, string output) = Run(["--columns", text]);

        Assert.True(exitCode == 0, output);
        Assert.Equal(columns.ToString(CultureInfo.InvariantCulture), output.Trim());
    }

    [Fact]
    public void The_report_measures_every_shipped_locale_against_English_in_columns()
    {
        (int exitCode, string output) = Run([]);
        Assert.True(exitCode == 0, output);
        string[] lines = output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.StartsWith("English: ", lines[0], StringComparison.Ordinal);

        string localization = RepoPaths.Source(Path.Combine("src", "DiffView.Avalonia", "Localization"));
        string[] shipped =
        [
            .. Directory.GetFiles(localization, "Strings.*.resx")
                .Select(path => Path.GetFileName(path)["Strings.".Length..^".resx".Length]),
        ];
        Assert.NotEmpty(shipped);

        foreach (string culture in shipped)
        {
            // A row: the culture, the widest entry's columns, its ratio to English, how many entries
            // are wider than English's widest, and the widest entry itself. Only the first two cells
            // are read — the entry's text reaches this process in the console's encoding, which on
            // Windows is not UTF-8.
            string row = Assert.Single(lines, l => l.StartsWith(culture + " ", StringComparison.Ordinal));
            int widest = int.Parse(row.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture);

            // The CJK locales' menu entries are wide glyphs, so their widest in columns exceeds their
            // longest in characters; a report counting characters would give exactly that longest.
            if (culture is "ja-JP" or "ko-KR" or "zh-CN")
            {
                int longest = XDocument.Load(Path.Combine(localization, $"Strings.{culture}.resx")).Root!
                    .Elements("data")
                    .Where(d => ((string?)d.Attribute("name"))?.StartsWith("Menu.", StringComparison.Ordinal) == true)
                    .Max(d => new StringInfo((string?)d.Element("value") ?? string.Empty).LengthInTextElements);
                Assert.True(
                    widest > longest,
                    $"{culture}'s widest menu entry measured {widest} columns, where its longest is {longest} characters: {row}");
            }
        }
    }

    private static (int ExitCode, string Output) Run(string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = "dotnet",
            WorkingDirectory = RepoPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add("run");
        start.ArgumentList.Add(Path.Combine("scripts", "measure-menu-width.cs"));
        start.ArgumentList.Add("--");
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start dotnet run.");

        // Both pipes drained concurrently: stdout to EOF first deadlocks once the child fills the
        // stderr pipe buffer, and the test then hangs rather than failing.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
