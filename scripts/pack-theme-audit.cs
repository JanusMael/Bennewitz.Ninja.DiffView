// scripts/pack-theme-audit.cs — a .NET 10 file-based app.
//
// Packs the theme-audit dotnet tool (Bennewitz.Ninja.ThemeAudit) into the local NuGet feed that
// NuGet.config lists as "../nuget-local", so another repository can adopt it as a local tool:
//
//   dotnet new tool-manifest            (once, in that repository)
//   dotnet tool install Bennewitz.Ninja.ThemeAudit
//   dotnet tool run theme-audit report
//
// Run from the repository root; the .sh and .ps1 wrappers beside this file do that for you.
//
//   dotnet run scripts/pack-theme-audit.cs
//   dotnet run scripts/pack-theme-audit.cs -- --feed <directory>

using System.Diagnostics;

const string ProjectPath = "src/ThemeAudit/ThemeAudit.csproj";

// The project sets no package version (the AutoVersioning generator stamps assemblies, not
// packages), so this script stamps it. Bump it when the tool's commands or output change.
const string PackageVersion = "1.1.0";

string feed = Path.GetFullPath(Path.Combine("..", "nuget-local"));
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--feed" && i + 1 < args.Length)
    {
        feed = Path.GetFullPath(args[++i]);
    }
}

if (!File.Exists(ProjectPath))
{
    Console.Error.WriteLine($"pack-theme-audit: {ProjectPath} not found — run from the repository root.");
    return 2;
}

Directory.CreateDirectory(feed);
Console.WriteLine($"pack-theme-audit: {Path.GetFullPath(ProjectPath)} → {feed}");

ProcessStartInfo start = new("dotnet") { UseShellExecute = false };
foreach (string argument in new[] { "pack", ProjectPath, "-c", "Release", "-o", feed, $"-p:PackageVersion={PackageVersion}", "--nologo" })
{
    start.ArgumentList.Add(argument);
}

using Process process = Process.Start(start) ?? throw new InvalidOperationException("dotnet could not be started.");
process.WaitForExit();
if (process.ExitCode != 0)
{
    Console.Error.WriteLine($"pack-theme-audit: dotnet pack failed ({process.ExitCode}).");
    return process.ExitCode;
}

foreach (string package in Directory.EnumerateFiles(feed, "Bennewitz.Ninja.ThemeAudit.*.nupkg").Order(StringComparer.Ordinal))
{
    Console.WriteLine($"  {Path.GetFileName(package)}");
}

return 0;
