using System.CommandLine;
using Bennewitz.Ninja.ThemeAudit;

RootCommand root = new("theme-audit — finds the resource keys an Avalonia theme leaves undefined and the tokens below a contrast floor.");

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

return root.Parse(args).Invoke();
