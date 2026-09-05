// scripts/pack-diagnostics.cs — a .NET 10 file-based app.
//
// Packs ClaudeForge's LayeredEditors.Avalonia.Diagnostics into the local NuGet feed that
// NuGet.config lists as "../nuget-local". The ClaudeForge checkout is the manifest's local
// path when it exists, otherwise reference/ClaudeForge (fetch it first). Run from the
// repository root; the .sh and .ps1 wrappers beside this file do that for you.
//
//   dotnet run scripts/pack-diagnostics.cs
//   dotnet run scripts/pack-diagnostics.cs -- --feed <directory>

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

const string ManifestPath = "reference/sources.json";
const string ProjectRelativePath = "src/LayeredEditors.Avalonia.Diagnostics/LayeredEditors.Avalonia.Diagnostics.csproj";

string feed = Path.GetFullPath(Path.Combine("..", "nuget-local"));
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--feed" && i + 1 < args.Length)
    {
        feed = Path.GetFullPath(args[++i]);
    }
}

if (!File.Exists(ManifestPath))
{
    Console.Error.WriteLine($"pack-diagnostics: {ManifestPath} not found — run from the repository root.");
    return 2;
}

Manifest manifest = JsonSerializer.Deserialize(File.ReadAllText(ManifestPath), ManifestJsonContext.Default.Manifest)
                    ?? throw new InvalidOperationException("The manifest is empty.");
Source? claudeForge = manifest.Sources.Find(s => string.Equals(s.Name, "ClaudeForge", StringComparison.OrdinalIgnoreCase));
if (claudeForge is null)
{
    Console.Error.WriteLine("pack-diagnostics: the manifest has no ClaudeForge entry.");
    return 2;
}

string? checkout = claudeForge.Local is not null && Directory.Exists(claudeForge.Local)
    ? claudeForge.Local
    : Directory.Exists(claudeForge.Dir) ? claudeForge.Dir : null;
if (checkout is null)
{
    Console.Error.WriteLine("pack-diagnostics: no ClaudeForge checkout — run: dotnet run scripts/fetch-reference.cs -- ClaudeForge");
    return 1;
}

string project = Path.Combine(checkout, ProjectRelativePath);
if (!File.Exists(project))
{
    Console.Error.WriteLine($"pack-diagnostics: {project} not found.");
    return 1;
}

Directory.CreateDirectory(feed);
Console.WriteLine($"pack-diagnostics: {Path.GetFullPath(checkout)} → {feed}");

// PackageReadmeFile is set in that csproj but the README does not ship; clear it for the pack.
ProcessStartInfo start = new("dotnet") { UseShellExecute = false };
foreach (string argument in new[] { "pack", project, "-c", "Release", "-o", feed, "-p:PackageReadmeFile=", "--nologo" })
{
    start.ArgumentList.Add(argument);
}

using Process process = Process.Start(start) ?? throw new InvalidOperationException("dotnet could not be started.");
process.WaitForExit();
if (process.ExitCode != 0)
{
    Console.Error.WriteLine($"pack-diagnostics: dotnet pack failed ({process.ExitCode}).");
    return process.ExitCode;
}

foreach (string package in Directory.EnumerateFiles(feed, "LayeredEditors.Avalonia.Diagnostics.*.nupkg"))
{
    Console.WriteLine($"  {Path.GetFileName(package)}");
}

return 0;

sealed record Manifest(List<Source> Sources);

sealed record Source(string Name, string Dir, string? Url, string? Ref, string? Local, List<string>? Sparse);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Manifest))]
partial class ManifestJsonContext : JsonSerializerContext;
