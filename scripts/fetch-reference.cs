// scripts/fetch-reference.cs — a .NET 10 file-based app.
//
// Fetches the read-only upstream checkouts listed in reference/sources.json, shallow and at
// their pinned ref, sparse where an entry asks for it. Run from the repository root; the
// .sh and .ps1 wrappers beside this file do that for you.
//
//   dotnet run scripts/fetch-reference.cs                        clone every missing source
//   dotnet run scripts/fetch-reference.cs -- --update            also re-fetch sources not at their pin
//   dotnet run scripts/fetch-reference.cs -- --status            report only; exit 1 if anything is missing or off-pin
//   dotnet run scripts/fetch-reference.cs -- Semi.Avalonia       limit to the named sources
//
// An entry with a "local" path is satisfied by that path when it exists (a sibling checkout
// the developer already has) and is otherwise cloned like the rest.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

const string ManifestPath = "reference/sources.json";
const string PinFile = ".fetched-ref";
const string Help = """
    fetch-reference — fetch the read-only upstream checkouts listed in reference/sources.json

      dotnet run scripts/fetch-reference.cs                  clone every missing source at its pinned ref
      dotnet run scripts/fetch-reference.cs -- --update      also re-fetch sources that are not at their pin
      dotnet run scripts/fetch-reference.cs -- --status      report only; exit 1 if anything is missing or off-pin
      dotnet run scripts/fetch-reference.cs -- --quiet       report only changes and problems
      dotnet run scripts/fetch-reference.cs -- <name> ...    limit to the named sources

    Run from the repository root (scripts/fetch-reference.sh and .ps1 do that for you).
    """;

bool update = false;
bool statusOnly = false;
bool quiet = false;
List<string> only = [];

foreach (string arg in args)
{
    switch (arg)
    {
        case "--update":
            update = true;
            break;
        case "--status":
            statusOnly = true;
            break;
        case "--quiet":
            quiet = true;
            break;
        case "-h" or "--help":
            Console.WriteLine(Help);
            return 0;
        default:
            only.Add(arg);
            break;
    }
}

if (!File.Exists(ManifestPath))
{
    Console.Error.WriteLine($"fetch-reference: {ManifestPath} not found — run from the repository root.");
    return 2;
}

Manifest manifest = JsonSerializer.Deserialize(File.ReadAllText(ManifestPath), ManifestJsonContext.Default.Manifest)
                    ?? throw new InvalidOperationException("The manifest is empty.");

int failures = 0;
foreach (Source source in manifest.Sources)
{
    if (only.Count > 0 && !only.Contains(source.Name, StringComparer.OrdinalIgnoreCase))
    {
        continue;
    }

    if (source.Local is not null && Directory.Exists(source.Local))
    {
        Report(source.Name, "local", Path.GetFullPath(source.Local));
        continue;
    }

    if (source.Url is null || source.Ref is null)
    {
        Report(source.Name, "invalid", "entry needs url and ref, or an existing local path");
        failures++;
        continue;
    }

    string pinPath = Path.Combine(source.Dir, PinFile);
    bool present = Directory.Exists(Path.Combine(source.Dir, ".git"));
    string? fetchedRef = present && File.Exists(pinPath) ? File.ReadAllText(pinPath).Trim() : null;
    bool onPin = present && string.Equals(fetchedRef, source.Ref, StringComparison.Ordinal);

    if (present && onPin)
    {
        Report(source.Name, "present", $"{source.Ref} → {source.Dir}");
        continue;
    }

    if (statusOnly || (present && !update))
    {
        string detail = present
            ? $"checkout is at '{fetchedRef ?? "?"}', manifest pins '{source.Ref}' — run with --update"
            : "run: dotnet run scripts/fetch-reference.cs";
        Report(source.Name, present ? "off-pin" : "missing", detail);
        failures++;
        continue;
    }

    if (present)
    {
        Report(source.Name, "refresh", $"removing {source.Dir}");
        DeleteTree(source.Dir);
    }

    try
    {
        Clone(source);
        File.WriteAllText(pinPath, source.Ref + Environment.NewLine);
        Report(source.Name, "fetched", $"{source.Ref} → {source.Dir}");
    }
    catch (Exception ex) when (ex is InvalidOperationException or IOException)
    {
        Report(source.Name, "failed", ex.Message);
        failures++;
    }
}

return failures == 0 ? 0 : 1;

void Report(string name, string state, string detail)
{
    if (quiet && state is "present" or "local")
    {
        return;
    }

    Console.WriteLine($"{name,-16} {state,-8} {detail}");
}

void Clone(Source source)
{
    string url = source.Url!;
    string reference = source.Ref!;
    string? parent = Path.GetDirectoryName(Path.GetFullPath(source.Dir));
    if (parent is not null)
    {
        Directory.CreateDirectory(parent);
    }

    bool isCommit = reference.Length >= 7 && reference.All(Uri.IsHexDigit);
    bool sparse = source.Sparse is { Count: > 0 };

    if (!isCommit)
    {
        List<string> arguments = ["clone", "--quiet", "--depth", "1", "--branch", reference];
        if (sparse)
        {
            arguments.AddRange(["--filter=blob:none", "--sparse"]);
        }

        arguments.AddRange([url, source.Dir]);
        Git(null, arguments);
    }
    else
    {
        // A commit cannot be cloned by name; init, fetch exactly that commit, check it out detached.
        Git(null, ["init", "--quiet", source.Dir]);
        Git(source.Dir, ["remote", "add", "origin", url]);
        if (sparse)
        {
            Git(source.Dir, ["sparse-checkout", "init", "--cone"]);
            Git(source.Dir, ["fetch", "--quiet", "--depth", "1", "--filter=blob:none", "origin", reference]);
        }
        else
        {
            Git(source.Dir, ["fetch", "--quiet", "--depth", "1", "origin", reference]);
        }

        Git(source.Dir, ["checkout", "--quiet", "--detach", "FETCH_HEAD"]);
    }

    if (sparse)
    {
        Git(source.Dir, ["sparse-checkout", "set", .. source.Sparse!]);
    }
}

static void Git(string? workingDirectory, IEnumerable<string> arguments)
{
    ProcessStartInfo start = new("git")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    if (workingDirectory is not null)
    {
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(workingDirectory);
    }

    foreach (string argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }

    using Process process = Process.Start(start)
                            ?? throw new InvalidOperationException("git could not be started — is it installed and on PATH?");
    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
    string stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    _ = stdout.Result;

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed ({process.ExitCode}): {stderr.Trim()}");
    }
}

static void DeleteTree(string path)
{
    // Git object files are read-only; clear the attribute so the delete succeeds on Windows too.
    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
    {
        File.SetAttributes(file, FileAttributes.Normal);
    }

    Directory.Delete(path, recursive: true);
}

sealed record Manifest(List<Source> Sources);

sealed record Source(string Name, string Dir, string? Url, string? Ref, string? Local, List<string>? Sparse);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Manifest))]
partial class ManifestJsonContext : JsonSerializerContext;
