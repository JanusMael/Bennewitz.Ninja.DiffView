using System.CommandLine;
using Bennewitz.Ninja.ThemeAudit;

RootCommand root = new("theme-audit — finds the resource keys an Avalonia theme leaves undefined and the tokens below a contrast floor.");

// ---- inventory: the quick per-directory key count -------------------------------------------

Argument<DirectoryInfo> pathArgument = new("path")
{
    Description = "Directory containing .axaml/.xaml files (a theme checkout, or a project's templates).",
};
pathArgument.AcceptExistingOnly();

Command inventory = new("inventory", "List the resource keys (x:Key) defined under a directory, per file and in total.");
inventory.Arguments.Add(pathArgument);
inventory.SetAction(parseResult =>
{
    DirectoryInfo directory = parseResult.GetValue(pathArgument)!;

    IReadOnlyList<ResourceKey> keys;
    try
    {
        keys = ResourceKeyScanner.Scan(directory.FullName);
    }
    catch (InvalidDataException ex)
    {
        Console.Error.WriteLine($"theme-audit: {ex.Message}");
        return 1;
    }

    foreach (IGrouping<string, ResourceKey> file in keys.GroupBy(k => k.File).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"{file.Count(),6}  {Path.GetRelativePath(directory.FullName, file.Key)}");
    }

    Console.WriteLine();
    Console.WriteLine($"{keys.Count} keys, {keys.Select(k => k.Key).Distinct(StringComparer.Ordinal).Count()} unique, in {keys.Select(k => k.File).Distinct(StringComparer.Ordinal).Count()} files under {directory.FullName}");
    return 0;
});

root.Subcommands.Add(inventory);

// ---- report: the configured audit -----------------------------------------------------------

Option<FileInfo> configOption = new("--config", "-c")
{
    Description = "The audit configuration (themes, consumers, compat).",
    DefaultValueFactory = _ => new FileInfo("theme-audit.json"),
};

Option<string?> outputOption = new("--output", "-o")
{
    Description = "Where to write the report; the configuration's 'report' path when omitted.",
};

Option<bool> checkOption = new("--check")
{
    Description = "Write nothing; exit 1 when the committed output differs from a fresh run (the drift check).",
};

Command report = new("report", "Inventory the configured themes, scan the consumers, and write the Markdown report.");
report.Options.Add(configOption);
report.Options.Add(outputOption);
report.Options.Add(checkOption);
report.SetAction(parseResult =>
{
    FileInfo configFile = parseResult.GetValue(configOption)!;
    string? outputOverride = parseResult.GetValue(outputOption);
    bool check = parseResult.GetValue(checkOption);

    try
    {
        AuditConfig config = AuditConfig.Load(configFile.FullName);
        AuditResult result = AuditRunner.Run(config);
        string markdown = MarkdownReport.Render(result);
        string output = outputOverride is not null ? Path.GetFullPath(outputOverride) : config.Resolve(config.Report);

        int undefined = result.Findings.Sum(f => f.Undefined.Select(u => u.Key).Distinct(StringComparer.Ordinal).Count());
        int lowContrast = result.Findings.Sum(f => f.Contrast.Count(c => c.Status == ContrastStatus.Fail));
        string summary = $"{result.Themes.Count} themes, {result.Consumers.Count} consumers, {undefined} undefined-key findings, {lowContrast} low-contrast findings";

        if (check)
        {
            if (!File.Exists(output))
            {
                Console.Error.WriteLine($"theme-audit: {output} does not exist; run `theme-audit report` to create it.");
                return 1;
            }

            if (Normalize(File.ReadAllText(output)) != Normalize(markdown))
            {
                Console.Error.WriteLine($"theme-audit: {output} is stale — it differs from a fresh run ({summary}); regenerate it with `theme-audit report`.");
                return 1;
            }

            Console.WriteLine($"theme-audit: {output} is up to date ({summary}).");
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, markdown);
        Console.WriteLine($"theme-audit: wrote {output} ({summary}).");
        return 0;
    }
    catch (AuditConfigException ex)
    {
        Console.Error.WriteLine($"theme-audit: {ex.Message}");
        return 2;
    }
    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"theme-audit: {ex.Message}");
        return 1;
    }
});

root.Subcommands.Add(report);

return root.Parse(args).Invoke();

static string Normalize(string text)
{
    return text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
