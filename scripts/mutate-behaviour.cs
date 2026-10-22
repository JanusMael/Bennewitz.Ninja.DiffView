// scripts/mutate-behaviour.cs — a .NET 10 file-based app.
//
// Proves a BEHAVIOUR test able to fail, by breaking the thing it guards and checking that it goes
// red — and that it goes red in the right place. `scripts/mutate-gates` does this for the
// static-analysis gates; this is its sibling for tests that drive the code and assert what it did.
//
// Why two harnesses and not one. `mutate-gates` derives its guards from the gate files and judges a
// mutation by which assertion was the FIRST to fail, which is the right question for a gate whose
// every line is an assertion over a derived population. A behaviour test is a scenario: what has to
// hold is that breaking the production code reddens it, that breaking a DIFFERENT part of the
// production code reddens a different stage of it, and that a change which should not matter leaves
// it green. Those are different declarations, so they are compared differently — and bolting a
// second judgement model into a 90 KB harness scoped to the gates would make both harder to read.
//
// A mutation SET is either the built-in one at the foot of this file — the library's edit-under-fold
// scenario — or a JSON file committed beside the tests it proves, named with --set. The machinery is
// here once; what a step of a plan proves is data beside that step's tests, so a proof can be run
// again a month later instead of being re-made from a description of it.
//
//   dotnet run scripts/mutate-behaviour.cs -- --list                     name them and exit, running nothing
//   dotnet run scripts/mutate-behaviour.cs --                            run them all
//   dotnet run scripts/mutate-behaviour.cs -- --only refold              substring match on the name
//   dotnet run scripts/mutate-behaviour.cs -- --set <file.json> --check  every edit applies once, every killer is declared; runs nothing
//   dotnet run scripts/mutate-behaviour.cs -- --set <file.json>          run a committed set
//   dotnet run scripts/mutate-behaviour.cs -- --judge <log> red [--killer <test>] [--says <text>]
//                                                                        judge a captured run, run nothing
//   dotnet run scripts/mutate-behaviour.cs -- --set <file.json> --kills <kills.json>
//                                                                        judge a set's tests against recorded kills, run nothing
//   dotnet run scripts/mutate-behaviour.cs -- --set <a.json> --set <b.json>
//                                                                        several sets, one after another, each judged on its own
//   dotnet run scripts/mutate-behaviour.cs -- --owed <ref>               the committed sets a change since <ref> owes a run
//   dotnet run scripts/mutate-behaviour.cs -- --parallel 4 [--set …]     every mutation in 4 worktrees at once, judged as one run
//   dotnet run scripts/mutate-behaviour.cs -- --set <file.json> --shard 2/4 --emit <file.tsv>
//                                                                        one shard, its verdicts written for --merge
//   dotnet run scripts/mutate-behaviour.cs -- --set <file.json> --merge <file.tsv> --merge …
//                                                                        judge shards' results as one run of the set
//
// ⭐ --parallel cuts N detached worktrees from HEAD under the temp directory and gives each the state of
// this checkout — uncommitted work included, because this harness exists to prove a test before it is
// committed. That state is written as a git TREE through a temporary index: no commit is made and no
// ref moves. Each worktree runs every Nth mutation of each set, from the binary that started it, so the
// harness's own set needs no copy there; each shard writes the tree it ran over, and the merge refuses
// anything but every declared mutation exactly once from shards of one N over that one tree — which is
// also the proof that each worktree was this checkout. It never edits this checkout, so this checkout
// may be edited while it runs.
//
// ⭐ --owed <ref> runs only the sets a change owes: a set is owed when its own file, a file one of its
// mutations edits, one of its test files or its pins changed since <ref>, committed or not. Without
// --set it chooses among every set under tests/*/Mutations/.
//
// ⚠ Two trims, each measured before it was made (2026-10-20, AgentBaseline): a mutation's build skips
// the restore the baseline already did, 6.4 s to 4.5; and a mutation that names its killer runs only the
// classes of the set's test files, 6.0 s to 1.7 for one class. A green mutation, and a red one that names
// no killer, still run the set's whole filter: "every test stays green" must mean every test, and a red
// with no killer is judged by whichever test fails. Narrowing can only relabel a failure — a kill outside
// the set's files reads as `survived` where it read as `wrong-killer` — never turn one into a pass.
//
// ⭐ `Expect` is COMPARED, not printed. A mutation that was meant to stay green and went red is as
// much a failure as one that was meant to go red and survived: the first says the test is asserting
// something it was never meant to, and plan 00031's own history has an example — a scratch mutation
// that "survived" because the generator caught its own exception as a fault boundary and the frame
// still rendered. The test was green and the code was broken.
//
// ⭐ A red mutation may name its KILLER — the test that must be among the failures — and what the
// run must SAY. Each way of looking like a kill without being the declared one fails the run: a kill
// by a different test (`wrong-killer`), the declared test failing on some other assertion
// (`wrong-assertion`), and a host that died with no test failed at all (`aborted`, which a reading of
// "not Passed!" alone would call a kill).
//
// ⭐ A set that names its TEST FILES is held to every test in them: a full run fails while any of
// those tests was killed by no mutation, because a test nothing can break proves nothing. A run
// narrowed with --only proves only what it reached and judges nothing about the rest.
//
// ⭐ A test no mutation of ours CAN break — one that asks about a dependency rather than about our code
// — is marked, not claimed: a line in its body reading `// inert-at-pin: <package> <version>`, the
// idiom AGENTS.md §5 sets for the gates, excuses it while the set's pins hold that package at that
// version. The pins are `Directory.Packages.props` unless the set names a file of its own. A moved pin
// is a setup fault, before anything runs; a mutation that kills a marked test anyway fails the run,
// because the marker is then the defect; and a marker in no test the reader found is a fault too,
// because the likeliest reason for one is a test the reader lost.
//
// ⛔ Each edit is reverted from a SNAPSHOT of the file's bytes taken before it, written back and
// compared, so this runs over uncommitted work — AGENTS.md §5 has a new test proven able to fail
// before it is committed. The snapshot is on disk as well, under the temp directory, and its path is
// printed before the first edit of that file, because a run killed mid-mutation leaves the file
// mutated and a revert from git would take the uncommitted work with it.
//
// ⛔ What a mutation may touch is DERIVED, not trusted: the whole repository's status, with a hash of
// every file it lists, is compared before and after every edit, and anything changed outside the
// declared file stops the run. Without that, an edit landing outside the scope reads as a no-op,
// survives the revert, and contaminates every mutation after it. The hash is what sees a file that
// was already modified being modified again, which a status line alone cannot.
//
// ⛔ The clean tree must pass FIRST: a baseline run before anything is edited, so a test that was
// already red cannot be read as killed.
//
// ⛔ Reverting a mutation does not UNBUILD it, so the project is rebuilt from a `finally` on every
// exit this program controls. A `dotnet test --no-build` straight after a killed run tests the last
// mutation's assemblies against reverted source.
//
// ⚠ A mutation whose edit matches nothing is a `no-op`, which FAILS the run. It is not a survivor and
// not a pass: the gate was never exercised, the code moved, and the mutation needs re-pointing. When a
// construct is deliberately designed out of existence its mutation is deleted and the plan says why.

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

string repo = Repo.Root();
List<string> only = [];
List<string> setFiles = [];
string? killsFile = null;
string? shardText = null;
string? parallelText = null;
string? emit = null;
List<string> merge = [];
string? owedRef = null;
string? owedBy = null;
bool list = false;
bool check = false;
string? judgeLog = null;
string? judgeExpect = null;
string? judgeKiller = null;
string? judgeSays = null;

// ⛔ Every flag is COLLECTED here and acted on below, none of them acting from inside the loop.
// An earlier version printed the list from inside it and returned, so `--only x --list` silently
// listed everything: a filter that does nothing is worse than one that errors, because the output
// looks like an answer. Only `--strayed` returns early, and it reads two files and runs nothing.
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--list":
            list = true;
            break;

        case "--check":
            check = true;
            break;

        case "--force":
            // Once the revert was `git checkout`, which needed a clean tree to be safe. A snapshot
            // revert needs none, so the flag has nothing left to do — and says so rather than
            // passing in silence.
            Console.WriteLine("mutate-behaviour: --force is no longer needed: every edit is reverted from a snapshot");
            break;

        case "--only" when i + 1 < args.Length:
            only.Add(args[++i]);
            break;

        case "--set" when i + 1 < args.Length:
            setFiles.Add(args[++i]);
            break;

        case "--kills" when i + 1 < args.Length:
            killsFile = args[++i];
            break;

        case "--shard" when i + 1 < args.Length:
            shardText = args[++i];
            break;

        case "--parallel" when i + 1 < args.Length:
            parallelText = args[++i];
            break;

        case "--emit" when i + 1 < args.Length:
            emit = Path.GetFullPath(args[++i]);
            break;

        case "--merge" when i + 1 < args.Length:
            merge.Add(Path.GetFullPath(args[++i]));
            break;

        case "--owed" when i + 1 < args.Length:
            owedRef = args[++i];
            break;

        // Runs no git: the changed paths are read from a file, one to a line. It is how the choice of
        // owed sets is proven, because what git says changed is the state of whatever checkout runs it.
        case "--owed-by" when i + 1 < args.Length:
            owedBy = args[++i];
            break;

        case "--judge" when i + 2 < args.Length:
            judgeLog = args[++i];
            judgeExpect = args[++i];
            break;

        case "--killer" when i + 1 < args.Length:
            judgeKiller = args[++i];
            break;

        case "--says" when i + 1 < args.Length:
            judgeSays = args[++i];
            break;

        // Runs nothing: two captured `git status` readings and the file a mutation declared. It is
        // how the stray-write guard is proven, because a real stray is not something a test can
        // arrange — the build would have to write into src or tests on its own.
        case "--strayed" when i + 3 < args.Length:
            string strayed = Git.Strayed(
                File.ReadAllText(args[i + 1]), File.ReadAllText(args[i + 2]), args[i + 3]);
            Console.WriteLine(strayed.Length == 0 ? "nothing strayed" : strayed);
            return strayed.Length == 0 ? 0 : 1;

        default:
            Console.Error.WriteLine($"mutate-behaviour: unknown argument `{args[i]}`");
            return 2;
    }
}

if (judgeLog is not null)
{
    if (judgeExpect is not ("red" or "green"))
    {
        Console.Error.WriteLine($"mutate-behaviour: --judge expects `red` or `green`, not `{judgeExpect}`");
        return 2;
    }

    string log = File.ReadAllText(judgeLog);
    Reading read = Judge.Read(log);
    string outcome = Judge.Outcome(read, new Declared(judgeExpect, judgeKiller, judgeSays), log);
    Console.WriteLine(read);
    Console.WriteLine(outcome);
    return outcome == Judge.Ok ? 0 : 1;
}

if (judgeKiller is not null || judgeSays is not null)
{
    Console.Error.WriteLine("mutate-behaviour: --killer and --says qualify --judge, and mean nothing without it");
    return 2;
}

// --shard k/n: every nth mutation from the kth, k counted from 1.
(int Index, int Count)? shard = null;
if (shardText is not null)
{
    string[] parts = shardText.Split('/');
    if (parts.Length != 2
        || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int shardIndex)
        || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int shardCount)
        || shardCount < 1 || shardIndex < 1 || shardIndex > shardCount)
    {
        Console.Error.WriteLine($"mutate-behaviour: --shard takes k/n with 1 <= k <= n, not `{shardText}`");
        return 2;
    }

    shard = (shardIndex, shardCount);
}

int? parallel = null;
if (parallelText is not null)
{
    if (!int.TryParse(parallelText, NumberStyles.None, CultureInfo.InvariantCulture, out int workers) || workers < 1)
    {
        Console.Error.WriteLine($"mutate-behaviour: --parallel takes a number of worktrees, at least 1, not `{parallelText}`");
        return 2;
    }

    parallel = workers;
}

// Each of these chooses or judges the mutations in a way the others contradict, so a combination is
// refused rather than resolved by a precedence nobody would guess.
string? clash =
    parallel is not null && (shard is not null || merge.Count > 0 || only.Count > 0 || list || check || killsFile is not null || emit is not null)
        ? "--parallel runs every mutation of each set and judges them together; it combines with --set and --owed alone"
    : merge.Count > 0 && (shard is not null || only.Count > 0 || list || check || killsFile is not null || emit is not null || setFiles.Count > 1)
        ? "--merge judges what one set's shards wrote; it combines with one --set alone"
    : shard is not null && only.Count > 0
        ? "--shard and --only each choose a subset, and a merge needs shards of the whole set"
    : killsFile is not null && (shard is not null || emit is not null)
        ? "--kills judges recorded kills and runs nothing, so there is no shard to choose and nothing to emit"
    : owedRef is not null && owedBy is not null
        ? "--owed asks git what changed and --owed-by reads it from a file; give one of them"
    : null;
if (clash is not null)
{
    Console.Error.WriteLine($"mutate-behaviour: {clash}");
    return 2;
}

// --owed: of the sets named, or else of every committed one, those a change owes a run.
if (owedRef is not null || owedBy is not null)
{
    IReadOnlyList<string> changed;
    List<string> candidates;
    try
    {
        changed = owedBy is not null ? [.. File.ReadAllLines(owedBy).Where(l => l.Trim().Length > 0)] : Git.Changed(repo, owedRef!);
        candidates = setFiles.Count > 0 ? [.. setFiles] : [.. Owed.CommittedSets(repo)];
    }
    catch (Exception ex) when (Unreadable(ex))
    {
        Console.Error.WriteLine($"mutate-behaviour: what changed cannot be read: {ex.Message}");
        return 2;
    }

    setFiles.Clear();
    foreach (string candidate in candidates)
    {
        string? because;
        try
        {
            because = Owed.Because(repo, candidate, Plan.Load(repo, candidate), changed);
        }
        catch (Exception ex) when (Unreadable(ex))
        {
            Console.Error.WriteLine($"mutate-behaviour: the set {candidate} cannot be read: {ex.Message}");
            return 2;
        }

        Console.WriteLine(because is null ? $"not owed  {candidate}" : $"owed      {candidate} — {because} changed");
        if (because is not null)
        {
            setFiles.Add(candidate);
        }
    }

    if (setFiles.Count == 0)
    {
        Console.WriteLine("no set is owed a run");
        return 0;
    }
}

if (setFiles.Count > 1 && (only.Count > 0 || killsFile is not null || emit is not null || shard is not null))
{
    Console.Error.WriteLine("mutate-behaviour: several sets run one after another, each one whole; --only, --kills, --emit and --shard each name a part of one set");
    return 2;
}

// --merge: shards' results judged as one run of the set, against this checkout's state as it is now —
// so a merge over shards that ran over other code is refused rather than judged.
if (merge.Count > 0)
{
    string? merged = setFiles.Count == 1 ? setFiles[0] : null;
    try
    {
        Plan mergedPlan = merged is null ? Plan.BuiltIn : Plan.Load(repo, merged);
        return Shards.JudgeShards(merged, mergedPlan, mergedPlan.ReadTests(repo), merge, Source.Tree(repo));
    }
    catch (Exception ex) when (Unreadable(ex))
    {
        Console.Error.WriteLine($"mutate-behaviour: the set {merged} cannot be read: {ex.Message}");
        return 2;
    }
}

// --parallel: every set's setup is checked here, before a worktree is cut, so a set that cannot run
// costs nothing.
if (parallel is { } worktrees)
{
    List<(string? File, Plan Plan, Declarations Declared)> runs = [];
    foreach (string? each in setFiles.Count > 0 ? [.. setFiles] : new string?[] { null })
    {
        Plan eachPlan;
        IReadOnlyDictionary<string, string> eachPins;
        try
        {
            eachPlan = each is null ? Plan.BuiltIn : Plan.Load(repo, each);
            eachPins = Pin.HeldBy(Path.Combine(repo, eachPlan.Pins.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception ex) when (Unreadable(ex))
        {
            Console.Error.WriteLine($"mutate-behaviour: the set {each} or its pins cannot be read: {ex.Message}");
            return 2;
        }

        Declarations eachDeclared = eachPlan.ReadTests(repo);
        List<string> eachFaults = [.. Setup.Faults(repo, eachPlan.Mutations, eachPlan.Mutations, eachDeclared, eachPins, eachPlan.Tests.Count > 0)];
        foreach (string fault in eachFaults)
        {
            Console.WriteLine($"  setup  {Shards.Name(each)}: {fault}");
        }

        if (eachFaults.Count > 0)
        {
            return 1;
        }

        runs.Add((each, eachPlan, eachDeclared));
    }

    return Parallel.Run(repo, runs, worktrees);
}

if (setFiles.Count > 1)
{
    List<string> passed = [];
    if (list)
    {
        passed.Add("--list");
    }

    if (check)
    {
        passed.Add("--check");
    }

    return Self.EachSet(repo, setFiles, passed);
}

string? setFile = setFiles.Count == 1 ? setFiles[0] : null;

Plan plan;
try
{
    plan = setFile is null ? Plan.BuiltIn : Plan.Load(repo, setFile);
}
catch (Exception ex) when (Unreadable(ex))
{
    Console.Error.WriteLine($"mutate-behaviour: the set {setFile} cannot be read: {ex.Message}");
    return 2;
}

IReadOnlyDictionary<string, string> pins;
try
{
    pins = Pin.HeldBy(Path.Combine(repo, plan.Pins.Replace('/', Path.DirectorySeparatorChar)));
}
catch (Exception ex) when (Unreadable(ex))
{
    Console.Error.WriteLine($"mutate-behaviour: the pins {plan.Pins} cannot be read: {ex.Message}");
    return 2;
}

List<Mutation> chosen = only.Count == 0
    ? [.. plan.Mutations]
    : [.. plan.Mutations.Where(m => only.Any(o => m.Name.Contains(o, StringComparison.OrdinalIgnoreCase)))];

if (shard is { } part)
{
    chosen = [.. Shards.Of(plan.Mutations, part.Index, part.Count)];
}

// A shard may hold nothing — more worktrees than a set has mutations — and that is a result, not an error.
if (chosen.Count == 0 && shard is null)
{
    Console.Error.WriteLine("mutate-behaviour: --only matched no mutation. Try --list.");
    return 2;
}

if (list)
{
    // What a run filters to is read from the set's test files. A set naming one that is missing still
    // lists, each of its runs as the whole project; the setup check, which a listing does not reach,
    // refuses it before anything runs.
    IReadOnlyList<string> listedClasses = [];
    try
    {
        listedClasses = plan.ReadClasses(repo);
    }
    catch (Exception ex) when (Unreadable(ex))
    {
        Console.Error.WriteLine($"mutate-behaviour: the set's test files cannot be read, so each run is listed as the whole project: {ex.Message}");
    }

    foreach (Mutation m in chosen)
    {
        Console.WriteLine($"  {m.Name}");
        Console.WriteLine($"      {m.File}");
        Console.WriteLine($"      expects: {m.Describe()}");
        Console.WriteLine($"      runs: {m.Runs(listedClasses)}");
    }

    return 0;
}

// The setup check runs before every run, not only under --check: a mutation that cannot apply, or
// names a test that does not exist, would otherwise cost a build and a test run to report itself.
Declarations declared = plan.ReadTests(repo);
List<string> faults = [.. Setup.Faults(repo, chosen, plan.Mutations, declared, pins, plan.Tests.Count > 0)];
foreach (string fault in faults)
{
    Console.WriteLine($"  setup  {fault}");
}

int marked = declared.Tests.Count(t => t.InertAt.Count > 0);
Console.WriteLine(
    $"setup: {chosen.Count.ToString(CultureInfo.InvariantCulture)} mutation(s) over "
    + $"{declared.Tests.Count.ToString(CultureInfo.InvariantCulture)} declared test(s)"
    + (marked > 0 ? $" ({marked.ToString(CultureInfo.InvariantCulture)} marked inert)" : string.Empty)
    + $", {faults.Count.ToString(CultureInfo.InvariantCulture)} fault(s)");

if (faults.Count > 0)
{
    return 1;
}

// ⛔ A set whose mutations edit the script this harness is running from — its own set — cannot run from
// that script: the tests it judges run `dotnet run` on the file, which rebuilds the binary running here,
// held open by Windows, so every test goes red and every mutation reads as killed. A dry run only notes it.
string? ownRunner = Setup.EditsItsRunner(repo, chosen);
if (check)
{
    // What a run of this set would give every build and test over the environment they inherit, read
    // from the one place those runs read it.
    foreach ((string name, string value) in Dotnet.BuildEnvironment)
    {
        Console.WriteLine($"  note   every build and test run is given {name}={value}");
    }

    if (ownRunner is not null)
    {
        Console.WriteLine($"  note   {Setup.RunnerNote(ownRunner)}");
    }

    return 0;
}

bool narrowed = only.Count > 0 || shard is not null;

// Runs nothing past the setup: kills recorded in a file stand in for the runs that would have made
// them, and are judged exactly as a run's own are. It is how that judgement is proven, because a
// mutation killing a test marked inert is not something a test of this harness can arrange short of
// a full run.
if (killsFile is not null)
{
    // A judgement of no tests at all would hold, and say so.
    if (plan.Tests.Count == 0)
    {
        Console.Error.WriteLine("mutate-behaviour: --kills judges the tests a set's files declare, and this set names no test file");
        return 2;
    }

    Dictionary<string, string> recorded = new(StringComparer.Ordinal);
    try
    {
        string path = Path.IsPathRooted(killsFile) ? killsFile : Path.Combine(repo, killsFile);
        foreach ((string mutation, List<string> failed) in JsonSerializer.Deserialize(File.ReadAllText(path), MutationSetJson.Default.DictionaryStringListString) ?? [])
        {
            RecordKills(recorded, mutation, failed);
        }
    }
    catch (Exception ex) when (Unreadable(ex))
    {
        Console.Error.WriteLine($"mutate-behaviour: the kills {killsFile} cannot be read: {ex.Message}");
        return 2;
    }

    Completeness judged = Completeness.Of(declared.Tests, recorded, narrowed);
    judged.Print();
    Console.WriteLine();
    Console.WriteLine(judged.Summary);
    Console.WriteLine(judged.Holds
        ? "every declared test is accounted for"
        : "A DECLARED TEST WAS NEVER BROKEN, OR ONE MARKED INERT WAS");
    return judged.Holds ? 0 : 1;
}

if (ownRunner is not null)
{
    Console.Error.WriteLine($"mutate-behaviour: {Setup.RunnerNote(ownRunner)}");
    return 2;
}

// The state this run is over, taken before anything is edited: a shard's merge must find every shard
// over the one tree --parallel gave them.
string? ranOver = emit is null ? null : Source.Tree(repo);
string label = shard is { } of ? $"{of.Index.ToString(CultureInfo.InvariantCulture)}/{of.Count.ToString(CultureInfo.InvariantCulture)}" : "1/1";

if (chosen.Count == 0)
{
    Shards.Emit(emit, label, ranOver, setFile, chosen, new Dictionary<string, (Reading Reading, string Outcome)>());
    Console.WriteLine($"shard {label} of this set holds no mutation, so nothing was built or run");
    return 0;
}

Console.WriteLine($"at {Git.Head(repo)}, {plan.Project}{(plan.Class is null ? string.Empty : " " + plan.Class)}");

if (!Dotnet.Build(repo, plan.Project, out string baselineBuild))
{
    Console.Error.WriteLine("baseline: the clean tree does not build — nothing was mutated");
    Console.Error.WriteLine(Tail(baselineBuild));
    return 1;
}

// A mutation that names its killer runs only the classes of the set's test files — the trim the header
// measures, decided by Mutation.ClassesFrom, which --list prints. The baseline runs the set's own filter,
// the widest any mutation here is judged by.
IReadOnlyList<string> setClasses = plan.ReadClasses(repo);

Reading baseline = Judge.Read(Dotnet.Test(repo, plan.Project, plan.Class is { } setClass ? [setClass] : []));
if (baseline.Verdict != "green")
{
    Console.Error.WriteLine($"baseline: the clean tree is red — nothing was mutated. {baseline}");
    return 1;
}

Console.WriteLine("baseline: green");

Dictionary<string, (Reading Reading, string Outcome)> results = [];
Dictionary<string, string> killedBy = new(StringComparer.Ordinal);
Dictionary<string, string> backups = new(StringComparer.Ordinal);
bool started = false;
bool restoresHeld = true;
int index = 0;

try
{
    foreach (Mutation mutation in chosen)
    {
        index++;
        string path = Path.Combine(repo, mutation.File.Replace('/', Path.DirectorySeparatorChar));
        byte[] original = File.ReadAllBytes(path);
        Snapshot.Decode(original, out string before, out Encoding encoding);

        string progress = $"[{index.ToString(CultureInfo.InvariantCulture)}/{chosen.Count.ToString(CultureInfo.InvariantCulture)}]";
        int matches = Setup.Count(before, mutation.Old);
        if (matches != 1)
        {
            Console.WriteLine($"{progress} no-op  {mutation.Name} — matched {matches.ToString(CultureInfo.InvariantCulture)} times, not once");
            results[mutation.Name] = (Reading.NoOp(matches), "no-op");
            continue;
        }

        if (!backups.ContainsKey(path))
        {
            backups[path] = Snapshot.Keep(repo, path, original);
            Console.WriteLine($"  snapshot of {mutation.File}: {backups[path]}");
        }

        string statusBefore = Git.Fingerprint(repo);
        File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(before.Replace(mutation.Old, mutation.New, StringComparison.Ordinal))).ToArray());
        started = true;

        try
        {
            // Compared before the run, not after it: an edit that lands somewhere unexpected must stop
            // the run before it has spent minutes producing a verdict about the wrong tree.
            string stray = Git.Strayed(statusBefore, Git.Fingerprint(repo), mutation.File);
            if (stray.Length > 0)
            {
                Console.Error.WriteLine($"mutate-behaviour: `{mutation.Name}` wrote outside {mutation.File}:");
                Console.Error.WriteLine(stray);
                return 2;
            }

            // The baseline restored; a mutation's build restores again only when it edits what restore reads.
            if (!Dotnet.Build(repo, plan.Project, out string buildLog, restore: mutation.EditsTheBuild))
            {
                Console.WriteLine($"{progress} build  {mutation.Name} — the mutation does not compile");
                results[mutation.Name] = (Reading.Unbuildable(buildLog), "build");
                continue;
            }

            string log = Dotnet.Test(repo, plan.Project, mutation.ClassesFrom(setClasses));
            Reading reading = Judge.Read(log);
            RecordKills(killedBy, mutation.Name, reading.Failed);
            string outcome = Judge.Outcome(reading, mutation.Declared, log);
            results[mutation.Name] = (reading, outcome);
            Console.WriteLine(outcome == Judge.Ok
                ? $"{progress} ok     {mutation.Name}"
                : $"{progress} WRONG  {mutation.Name} — expected {mutation.Describe()}, got {outcome}");
        }
        finally
        {
            File.WriteAllBytes(path, original);
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
            {
                restoresHeld = false;
                Console.Error.WriteLine($"mutate-behaviour: the revert of {mutation.File} did not take — restore it from {backups[path]}");
            }
        }
    }
}
finally
{
    if (started)
    {
        // A revert leaves the mutation's assemblies in bin/. Anything run after this program without
        // a rebuild would test them.
        Console.WriteLine("rebuilding, because a revert does not unbuild a mutation");
        Dotnet.Build(repo, plan.Project, out _);
    }

    if (restoresHeld)
    {
        foreach (string backup in backups.Values)
        {
            File.Delete(backup);
        }
    }
}

Shards.Emit(emit, label, ranOver, setFile, chosen, results);
return Verdict.Report(chosen, results, declared, killedBy, narrowed, restoresHeld);

static string Tail(string log) => log.Length > 1200 ? log[^1200..] : log;

static bool Unreadable(Exception ex) =>
    ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or XmlException;

static void RecordKills(Dictionary<string, string> killedBy, string mutation, IEnumerable<string> failed) =>
    Verdict.RecordKills(killedBy, mutation, failed);

/// <summary>How a set's run is judged, once it has run — by this process or by shards that each ran a part of it.</summary>
internal static class Verdict
{
    /// <summary>
    /// The one place a kill is recorded, for a run's own kills, recorded ones and merged ones alike: each
    /// test a mutation failed, credited to the first mutation that failed it.
    /// </summary>
    public static void RecordKills(Dictionary<string, string> killedBy, string mutation, IEnumerable<string> failed)
    {
        foreach (string test in failed)
        {
            killedBy.TryAdd(test, mutation);
        }
    }

    /// <summary>Prints each mutation against its declaration and the set's tests against the kills, and answers the exit code.</summary>
    public static int Report(
        IReadOnlyList<Mutation> chosen,
        IReadOnlyDictionary<string, (Reading Reading, string Outcome)> results,
        Declarations declared,
        IReadOnlyDictionary<string, string> killedBy,
        bool narrowed,
        bool restoresHeld)
    {
        Console.WriteLine();
        bool ok = true;
        foreach (Mutation mutation in chosen)
        {
            if (!results.TryGetValue(mutation.Name, out (Reading Reading, string Outcome) r))
            {
                continue;
            }

            bool agree = r.Outcome == Judge.Ok;
            ok &= agree;
            Console.WriteLine($"{(agree ? "  ok  " : " WRONG")}  {mutation.Name}");
            if (agree)
            {
                continue;
            }

            Console.WriteLine($"          expected {mutation.Describe()}, got {r.Outcome}");
            if (r.Reading.Failed.Count > 0)
            {
                Console.WriteLine($"          failed: {string.Join(", ", r.Reading.Failed)}");
            }

            if (r.Reading.Message.Length > 0)
            {
                Console.WriteLine($"          {r.Reading.Message}");
            }
        }

        // ⭐ The half a set adds: every test it declares must have been broken by something, or be marked
        // inert at a pin the set still holds. A test no mutation reddened is a test nothing has shown able
        // to fail.
        Completeness judgement = Completeness.Of(declared.Tests, killedBy, narrowed);
        judgement.Print();

        Console.WriteLine();
        Console.WriteLine(
            $"mutations: {results.Count.ToString(CultureInfo.InvariantCulture)}, as declared: "
            + $"{results.Values.Count(v => v.Outcome == Judge.Ok).ToString(CultureInfo.InvariantCulture)}");
        if (declared.Tests.Count > 0)
        {
            Console.WriteLine(judgement.Summary);
        }

        Console.WriteLine(ok && judgement.Holds && restoresHeld
            ? "every mutation matched its declaration"
            : "A MUTATION DID NOT MATCH ITS DECLARATION, A DECLARED TEST WAS NEVER BROKEN, OR ONE MARKED INERT WAS — the tests do not guard what they claim");
        return ok && judgement.Holds && restoresHeld ? 0 : 1;
    }
}

/// <summary>What a mutation says must happen to the run it is judged by.</summary>
/// <param name="Expect">`red` or `green`.</param>
/// <param name="Killer">For a red mutation, the test that must be among the failures, or null.</param>
/// <param name="Says">Text the run must contain, or null.</param>
internal sealed record Declared(string Expect, string? Killer, string? Says);

/// <summary>What a run of the filtered tests said: the verdict, the tests that failed, and where the first trace stopped.</summary>
internal readonly record struct Reading(string Verdict, string Message, IReadOnlyList<int> Lines, IReadOnlyList<string> Failed)
{
    public static Reading NoOp(int matches) =>
        new("no-op", $"matched {matches.ToString(CultureInfo.InvariantCulture)} times, not once", [], []);

    public static Reading Unbuildable(string log) =>
        new("build", log.Length > 400 ? log[^400..] : log, [], []);

    public override string ToString()
    {
        string failed = Failed.Count == 0 ? string.Empty : $" [{string.Join(", ", Failed)}]";
        return Lines.Count == 0
            ? $"{Verdict} {Message}{failed}".TrimEnd()
            : $"{Verdict} {Message} at {string.Join(',', Lines)}{failed}".Trim();
    }
}

/// <summary>
/// Reads a `dotnet test` log. ⛔ The outcome line is load-bearing and no arithmetic replaces it: an
/// aborted host prints `Failed!` with `failed: 0`, which every count-based reading calls healthy —
/// `AGENTS.md` §5 and `scripts/catch-crash`. So anything that is not an explicit `Passed!` is red.
/// </summary>
internal static partial class Judge
{
    /// <summary>The one outcome that is not a failure of the run.</summary>
    public const string Ok = "ok";

    public static Reading Read(string log)
    {
        bool passed = log.Contains("Test run summary: Passed!", StringComparison.Ordinal);
        string message = string.Empty;
        foreach (string raw in log.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("Assert.", StringComparison.Ordinal))
            {
                message = line;
                break;
            }
        }

        List<int> lines = [];
        foreach (Match m in TraceLine().Matches(log))
        {
            int at = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!lines.Contains(at))
            {
                lines.Add(at);
            }
        }

        lines.Sort();

        List<string> failed = [];
        foreach (Match m in FailedTest().Matches(log))
        {
            string name = m.Groups["name"].Value;
            if (!failed.Contains(name))
            {
                failed.Add(name);
            }
        }

        return new Reading(passed ? "green" : "red", message, lines, failed);
    }

    /// <summary>
    /// Compares a reading against what was declared. ⛔ Every outcome but <see cref="Ok"/> fails the
    /// run, including the three that look like a kill: a red with no test failed by name is a host
    /// that died, a red that did not fail the declared killer was some other test's, and a killer
    /// that failed without the declared words failed on some other assertion.
    /// </summary>
    public static string Outcome(Reading read, Declared declared, string log)
    {
        if (declared.Expect == "green")
        {
            return read.Verdict == "green" ? Ok : "unexpected-red";
        }

        if (read.Verdict == "green")
        {
            return "survived";
        }

        if (read.Failed.Count == 0)
        {
            return "aborted";
        }

        if (declared.Killer is { } killer && !read.Failed.Contains(killer))
        {
            return "wrong-killer";
        }

        return declared.Says is { } says && !log.Contains(says, StringComparison.Ordinal)
            ? "wrong-assertion"
            : Ok;
    }

    /// <summary>A stack frame in a test file: the line its trace stopped on.</summary>
    [GeneratedRegex(@"Tests[\\/][^\s:]*\.cs:(\d+)")]
    private static partial Regex TraceLine();

    /// <summary>
    /// A failed test as Microsoft.Testing.Platform prints one: <c>failed</c>, the fully qualified
    /// name, then its duration in parentheses. The name is the last segment.
    /// </summary>
    [GeneratedRegex(@"(?m)^failed\s+\S*?\.(?<name>\w+)[\s(]")]
    private static partial Regex FailedTest();
}

/// <summary>What is checked before anything is edited, built or run.</summary>
internal static partial class Setup
{
    /// <param name="mutations">The mutations this run chose, which every edit-level check reads.</param>
    /// <param name="whole">Every mutation of the set, which the set-level rules read: a shard or an
    /// <c>--only</c> holds a part, and a part dealt no green mutation is not a set without one.</param>
    public static IEnumerable<string> Faults(
        string repo, IReadOnlyList<Mutation> mutations, IReadOnlyList<Mutation> whole, Declarations declared,
        IReadOnlyDictionary<string, string> pins, bool hasDeclaredTests)
    {
        // ⛔ A set's positive control (DECISIONS.md, 2026-10-22): a green run is the only one in which a
        // test failing under every mutation shows, each red one reading as its kill.
        if (whole.Any(m => m.Expect == "red") && !whole.Any(m => m.Expect == "green"))
        {
            yield return "this set holds red mutations and no green mutation, so a test that fails under every one of "
                + "them would read as killed by each — add a control: an edit that changes nothing, declared green with "
                + "its because";
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (Mutation m in mutations)
        {
            if (!names.Add(m.Name))
            {
                yield return $"\"{m.Name}\" is declared twice, and a name is how a mutation is found";
            }

            string path = Path.Combine(repo, m.File.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                yield return $"\"{m.Name}\" names a file that does not exist: {m.File}";
                continue;
            }

            Snapshot.Decode(File.ReadAllBytes(path), out string text, out _);
            int matches = Count(text, m.Old);
            if (matches != 1)
            {
                yield return $"\"{m.Name}\" matches {matches.ToString(CultureInfo.InvariantCulture)} times in {m.File}, not once";
            }

            if (hasDeclaredTests && m.Killer is { } killer && !declared.Declares(killer))
            {
                yield return $"\"{m.Name}\" names a killer the set's test files do not declare: {killer}";
            }
        }

        // ⛔ A marker is judged against its pin before anything runs, and in a dry run too: a moved pin
        // is exactly the change that lands green while the test it excused stops being excused.
        foreach (DeclaredTest test in declared.Tests)
        {
            foreach (Pin marker in test.InertAt)
            {
                string? pinned = pins.GetValueOrDefault(marker.Package);
                if (pinned != marker.Version)
                {
                    string now = pinned is null ? $"the pins name no {marker.Package}" : $"the pins hold it at {pinned}";
                    yield return $"{test.Name} is marked inert at {marker}, and {now} — re-measure it, then give it a mutation or mark it again";
                }
            }
        }

        foreach (string stray in declared.StrayMarkers)
        {
            yield return $"{stray} holds a marker no declared test does — it excuses nothing; put it in the body of the test it is "
                + "for, or find why the reader did not see that test";
        }

        // ⛔ The positive control on the reading itself: a file in which the reader finds no test would
        // pass every judgement made of its tests, a lost test reading as no test at all.
        foreach (string file in declared.Barren)
        {
            yield return $"{file} declares no test the reader can see, and a reading that finds nothing passes";
        }
    }

    public static int Count(string body, string needle)
    {
        int n = 0;
        for (int at = body.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = body.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            n++;
        }

        return n;
    }

    /// <summary>
    /// The tests a file declares — a public method whose block of attributes, read upward from it,
    /// holds a <c>Fact</c> or a <c>Theory</c>, Avalonia's or xUnit's, whatever else sits beside it —
    /// each with the pins its body marks it inert at; and the line of every marker no test's body holds.
    /// </summary>
    /// <remarks>
    /// ⛔ Read by lines, never by matching brackets: an attribute's arguments may hold a <c>]</c> of
    /// their own — <c>[InlineData("""{ "candidates": [ 3 ] }""")]</c> is plan 00034 step 4's — and a
    /// pattern for <c>[…]</c> stops at it and loses the test, which then reads as one no mutation is
    /// asked to kill rather than as a test the reader never saw. A body runs from the declaration to
    /// the first line closing it at the declaration's own indentation, which is how every test file
    /// here is laid out.
    /// </remarks>
    public static (IReadOnlyList<DeclaredTest> Tests, IReadOnlyList<int> StrayMarkers) TestsIn(string source)
    {
        string[] lines = source.Split('\n');
        List<(string Name, int From, int To)> bodies = [];
        for (int i = 0; i < lines.Length; i++)
        {
            Match method = TestMethod().Match(lines[i]);
            if (!method.Success)
            {
                continue;
            }

            for (int above = i - 1; above >= 0; above--)
            {
                string line = lines[above].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!line.StartsWith('['))
                {
                    break;
                }

                if (TestAttribute().IsMatch(line))
                {
                    bodies.Add((method.Groups["name"].Value, i, BodyEnd(lines, i, method.Groups["indent"].Value)));
                    break;
                }
            }
        }

        List<Pin>[] marks = [.. bodies.Select(_ => new List<Pin>())];
        List<int> strays = [];
        for (int i = 0; i < lines.Length; i++)
        {
            Match marker = InertMarker().Match(lines[i]);
            if (!marker.Success)
            {
                continue;
            }

            int holder = bodies.FindIndex(b => b.From < i && i < b.To);
            if (holder < 0)
            {
                strays.Add(i + 1);
                continue;
            }

            marks[holder].Add(new Pin(marker.Groups["package"].Value, marker.Groups["version"].Value));
        }

        return ([.. bodies.Select((b, at) => new DeclaredTest(b.Name, marks[at]))], strays);
    }

    /// <summary>
    /// The line closing the member declared on line <paramref name="at"/>: the first after it that holds
    /// only a brace at the declaration's own indentation, or the end of the file.
    /// </summary>
    private static int BodyEnd(string[] lines, int at, string indent)
    {
        for (int i = at + 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r') == indent + "}")
            {
                return i;
            }
        }

        return lines.Length;
    }

    /// <summary>
    /// The classes a test file declares, each qualified by the file's namespace, as <c>--filter-class</c>
    /// takes them. A nested class comes out with its own name alone, which no class has, so it filters in
    /// nothing; a file declaring none gives nothing, and a mutation judged by it then runs its set's whole filter.
    /// </summary>
    public static IEnumerable<string> ClassesIn(string source)
    {
        Match space = Namespace().Match(source);
        string prefix = space.Success ? space.Groups["name"].Value + "." : string.Empty;
        foreach (Match declared in ClassDeclaration().Matches(source))
        {
            yield return prefix + declared.Groups["name"].Value;
        }
    }

    /// <summary>
    /// The file among <paramref name="mutations"/>' that is the script this harness is running from, or
    /// null. A file-based app's entry file is in its runtimeconfig, so a binary launched directly — a shard
    /// of --parallel — still names the script it was built from, which is not its worktree's copy.
    /// </summary>
    public static string? EditsItsRunner(string repo, IEnumerable<Mutation> mutations)
    {
        if (AppContext.GetData("EntryPointFilePath") is not string entry)
        {
            return null;
        }

        StringComparison paths = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string running = Path.GetFullPath(entry);
        return mutations.Select(m => m.File).FirstOrDefault(
            f => string.Equals(Path.GetFullPath(Path.Combine(repo, f.Replace('/', Path.DirectorySeparatorChar))), running, paths));
    }

    public static string RunnerNote(string file) =>
        $"this set edits {file}, the script this harness is running from, and its tests rebuild that script's "
        + "binary while it runs — run the harness from a copy of the script, or with --parallel, whose shards "
        + "run from the binary that started them";

    [GeneratedRegex(@"(?m)^\s*namespace\s+(?<name>[\w.]+)")]
    private static partial Regex Namespace();

    [GeneratedRegex(@"(?m)^\s*(?:(?:public|internal|sealed|static|partial|abstract)\s+)*class\s+(?<name>\w+)")]
    private static partial Regex ClassDeclaration();

    [GeneratedRegex(@"^(?<indent>\s*)public\s+(?:async\s+Task|void)\s+(?<name>\w+)\s*\(")]
    private static partial Regex TestMethod();

    [GeneratedRegex(@"^\[(?:Avalonia)?(?:Fact|Theory)\b")]
    private static partial Regex TestAttribute();

    /// <summary>AGENTS.md §5's marker, spelled as <c>scripts/mutate-gates</c> reads it.</summary>
    [GeneratedRegex(@"^\s*//\s*inert-at-pin:\s*(?<package>\S+)\s+(?<version>\S+)\s*$")]
    private static partial Regex InertMarker();
}

/// <summary>A package at a version: what an inert-at-pin marker names, and what a set's pins hold.</summary>
internal readonly record struct Pin(string Package, string Version)
{
    public override string ToString() => $"{Package} {Version}";

    /// <summary>
    /// The version a props file pins each package at, read as central package management reads one:
    /// every <c>PackageVersion</c> element in any namespace, none inside a comment, and the package ids
    /// without regard to case, as NuGet's are.
    /// </summary>
    public static IReadOnlyDictionary<string, string> HeldBy(string path)
    {
        Dictionary<string, string> held = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement entry in XDocument.Load(path).Descendants().Where(e => e.Name.LocalName == "PackageVersion"))
        {
            if (entry.Attribute("Include")?.Value is { } package && entry.Attribute("Version")?.Value is { } version)
            {
                held.TryAdd(package, version);
            }
        }

        return held;
    }
}

/// <summary>A test a set's files declare, and every pin it is marked inert at — none, for a test some mutation must kill.</summary>
internal sealed record DeclaredTest(string Name, IReadOnlyList<Pin> InertAt);

/// <summary>
/// What a set's test files declare: their tests, where each marker sits that none of those tests holds,
/// and every file in which the reader found no test at all.
/// </summary>
internal sealed record Declarations(IReadOnlyList<DeclaredTest> Tests, IReadOnlyList<string> StrayMarkers, IReadOnlyList<string> Barren)
{
    public bool Declares(string test) => Tests.Any(t => t.Name == test);
}

/// <summary>
/// Whether every test a set declares was shown able to fail — killed by some mutation — or is marked
/// inert at a pin the set still holds and was killed by none.
/// </summary>
/// <remarks>
/// A test marked inert that a mutation killed anyway was never inert, and its marker is the defect.
/// That is judged in a narrowed run too, because a kill is evidence whatever else ran; a test killed by
/// none is judged only over the whole set, which is the only run that asked every mutation.
/// </remarks>
internal sealed record Completeness(
    IReadOnlyList<DeclaredTest> Killed,
    IReadOnlyList<DeclaredTest> Inert,
    IReadOnlyList<DeclaredTest> Unkilled,
    IReadOnlyList<(DeclaredTest Test, string By)> Belied,
    bool Narrowed)
{
    /// <param name="tests">The tests the set declares.</param>
    /// <param name="killedBy">Each test some mutation failed, and the first mutation that failed it.</param>
    /// <param name="narrowed">Whether the run was narrowed with --only.</param>
    public static Completeness Of(IReadOnlyList<DeclaredTest> tests, IReadOnlyDictionary<string, string> killedBy, bool narrowed) =>
        new(
            [.. tests.Where(t => t.InertAt.Count == 0 && killedBy.ContainsKey(t.Name))],
            [.. tests.Where(t => t.InertAt.Count > 0 && !killedBy.ContainsKey(t.Name))],
            [.. tests.Where(t => t.InertAt.Count == 0 && !killedBy.ContainsKey(t.Name))],
            [.. tests.Where(t => t.InertAt.Count > 0 && killedBy.ContainsKey(t.Name)).Select(t => (t, killedBy[t.Name]))],
            narrowed);

    public bool Holds => (Unkilled.Count == 0 || Narrowed) && Belied.Count == 0;

    public string Summary =>
        $"declared tests — {(Killed.Count + Inert.Count + Unkilled.Count + Belied.Count).ToString(CultureInfo.InvariantCulture)}: "
        + $"{Killed.Count.ToString(CultureInfo.InvariantCulture)} killed by a mutation, "
        + $"{Inert.Count.ToString(CultureInfo.InvariantCulture)} inert at the current pin, "
        + $"{Unkilled.Count.ToString(CultureInfo.InvariantCulture)} killed by none"
        + (Narrowed ? " (a narrowed run judges none of them)" : string.Empty)
        + $", {Belied.Count.ToString(CultureInfo.InvariantCulture)} with a bad marker";

    /// <summary>Every test a reader has to act on, or to know was excused, by name.</summary>
    public void Print()
    {
        if (Killed.Count + Inert.Count + Unkilled.Count + Belied.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("declared tests no mutation killed:");
        foreach (DeclaredTest test in Unkilled)
        {
            Console.WriteLine($"  {test.Name}");
        }

        if (Unkilled.Count == 0)
        {
            Console.WriteLine("  (none)");
        }

        if (Inert.Count > 0)
        {
            Console.WriteLine("declared tests excused, marked inert at a pin the set's pins still hold and killed by none:");
            foreach (DeclaredTest test in Inert)
            {
                Console.WriteLine($"  {test.Name}, inert at {string.Join(" and ", test.InertAt)}");
            }
        }

        if (Belied.Count > 0)
        {
            Console.WriteLine("declared tests marked inert that a mutation killed anyway — remove each marker:");
            foreach ((DeclaredTest test, string by) in Belied)
            {
                Console.WriteLine($"  {test.Name}, marked inert at {string.Join(" and ", test.InertAt)}, killed by \"{by}\"");
            }
        }
    }
}

/// <summary>A file's bytes kept aside before its first edit, and how its text is read and written.</summary>
internal static class Snapshot
{
    /// <summary>
    /// Writes <paramref name="original"/> under the temp directory and answers where — in a directory
    /// of this checkout's own, so two runs in two worktrees never share a snapshot's path.
    /// </summary>
    public static string Keep(string repo, string path, byte[] original)
    {
        string checkout = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(repo)))[..12];
        string directory = Path.Combine(Path.GetTempPath(), "mutate-behaviour", checkout);
        Directory.CreateDirectory(directory);
        string hash = Convert.ToHexString(SHA256.HashData(original))[..12];
        string backup = Path.Combine(directory, $"{Path.GetFileName(path)}.{hash}.orig");
        File.WriteAllBytes(backup, original);
        return backup;
    }

    /// <summary>
    /// The text of <paramref name="bytes"/>, and an encoding that writes it back with the same
    /// byte-order mark or none, so a mutation changes the edit it declares and nothing else.
    /// </summary>
    public static void Decode(byte[] bytes, out string text, out Encoding encoding)
    {
        bool marked = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: marked);
        text = new UTF8Encoding(false).GetString(bytes, marked ? 3 : 0, bytes.Length - (marked ? 3 : 0));
    }
}

internal static class Git
{
    public static string Head(string repo) =>
        Shell.Run(repo, "git", ["rev-parse", "--short", "HEAD"]).Output.Trim();

    /// <summary>
    /// The whole repository's status, each entry prefixed by a hash of the file it names. The hash is
    /// what sees an already-modified file modified again — its status line does not move — and the
    /// prefix keeps the entry ending in its path, which is how the declared file is recognised.
    /// ⛔ `--untracked-files=all`, not the default: `status.showUntrackedFiles=no` in a user's config
    /// would otherwise hide exactly the file a stray write created.
    /// </summary>
    public static string Fingerprint(string repo)
    {
        string status = Shell.Run(repo, "git", ["status", "--porcelain", "--untracked-files=all"]).Output;
        StringBuilder lines = new();
        foreach (string raw in status.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string entry = raw.TrimEnd('\r');
            string file = Path.Combine(repo, entry.Length > 3 ? entry[3..].Trim('"') : string.Empty);
            string hash = File.Exists(file)
                ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))[..12]
                : "------------";
            lines.Append(hash).Append(' ').Append(entry).Append('\n');
        }

        return lines.ToString();
    }

    /// <summary>
    /// Whatever appeared between two readings of the repository's status that the mutation did not
    /// declare. Pure, and separated from the reading so it can be proven: this is the one safety
    /// property this harness adds over the one-off it came from, and an untested guard is a claim.
    /// </summary>
    public static string Strayed(string before, string after, string declared)
    {
        HashSet<string> was = [.. before.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())];
        List<string> strayed = [];
        foreach (string line in after.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string entry = line.Trim();

            // The declared file is matched at the end of the status entry, past its ` M ` marker —
            // and only the declared one, so a second file the edit touched is a stray even when the
            // mutation meant to touch it.
            if (!was.Contains(entry) && !entry.EndsWith(declared, StringComparison.Ordinal))
            {
                strayed.Add(entry);
            }
        }

        return string.Join(Environment.NewLine, strayed);
    }

    /// <summary>
    /// Every path a change since <paramref name="since"/> touches, committed or not: what
    /// <c>git diff --name-only</c> reports against that ref, and each untracked file the ignore rules let
    /// through. Paths as git names them, relative to the repository with <c>/</c>.
    /// </summary>
    public static IReadOnlyList<string> Changed(string repo, string since)
    {
        Shell.Result diff = Shell.Run(repo, "git", ["diff", "--name-only", since, "--"]);
        Shell.Result untracked = Shell.Run(repo, "git", ["ls-files", "--others", "--exclude-standard"]);
        if (diff.Exit != 0 || untracked.Exit != 0)
        {
            throw new InvalidDataException($"git cannot say what changed since `{since}`: {(diff.Exit != 0 ? diff.Output : untracked.Output).Trim()}");
        }

        return [.. (diff.Stdout + "\n" + untracked.Stdout).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.Ordinal)];
    }
}

internal static class Dotnet
{
    /// <summary>How long one test run may take before it is killed and reported, rather than waited on for ever.</summary>
    private static readonly TimeSpan RunBound = TimeSpan.FromMinutes(10);

    /// <summary>
    /// What every build and test run this harness starts is given over the environment it inherits:
    /// Avalonia's build telemetry off (DECISIONS.md, 2026-10-22). The collector its build task starts
    /// before each compile outlived a build by three quarters of an hour and kept the worktree it ran in.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildEnvironment { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) { ["AVALONIA_TELEMETRY_OPTOUT"] = "1" };

    /// <param name="restore">False once a build has restored and nothing restore reads has changed since:
    /// measured, a mutation's build takes 4.5 s without it where it took 6.4.</param>
    public static bool Build(string repo, string project, out string log, bool restore = true)
    {
        Shell.Result result = Shell.Run(
            repo,
            "dotnet",
            restore
                ? ["build", project, "-c", "Debug", "--nologo", "-v", "q"]
                : ["build", project, "-c", "Debug", "--nologo", "-v", "q", "--no-restore"],
            RunBound,
            BuildEnvironment);
        log = result.Output;
        return result.Exit == 0;
    }

    /// <param name="classes">The classes to run, each its own <c>--filter-class</c>, which add up; none for the whole project.</param>
    public static string Test(string repo, string project, IReadOnlyList<string> classes) =>
        Shell.Run(
            repo,
            "dotnet",
            [
                "test", "--project", project, "--no-build", "-c", "Debug",
                .. classes.SelectMany(c => (string[])["--filter-class", c]),
            ],
            RunBound,
            BuildEnvironment).Output;
}

internal static class Shell
{
    /// <param name="Output">Standard output and standard error, in that order: what a reader of the run is shown.</param>
    /// <param name="Stdout">Standard output alone: what a caller parses, which a warning on standard error must not reach.</param>
    public readonly record struct Result(int Exit, string Output, string Stdout);

    public static Result Run(
        string repo, string tool, string[] arguments, TimeSpan? bound = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        ProcessStartInfo start = new(tool)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repo,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach ((string name, string value) in environment ?? new Dictionary<string, string>())
        {
            start.Environment[name] = value;
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"could not start {tool}");

        // Both pipes drained concurrently: reading one to its end first deadlocks once the child
        // fills the other's buffer.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(bound ?? Timeout.InfiniteTimeSpan))
        {
            // Bounded, and said: a run that is killed reports that it was, which no reading can
            // mistake for a pass — there is no `Passed!` in it.
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            string partial = stdout.GetAwaiter().GetResult();
            return new Result(-1, $"{partial}{stderr.GetAwaiter().GetResult()}\nmutate-behaviour: killed after {bound!.Value.TotalMinutes.ToString(CultureInfo.InvariantCulture)} minutes\n", partial);
        }

        process.WaitForExit();
        string output = stdout.GetAwaiter().GetResult();
        return new Result(process.ExitCode, output + stderr.GetAwaiter().GetResult(), output);
    }
}

internal static class Repo
{
    public static string Root()
    {
        for (DirectoryInfo? dir = new(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DiffView.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("not inside the DiffView repository: run from it, as the wrappers do");
    }
}

/// <param name="Name">What is broken, in the words of the contract it breaks.</param>
/// <param name="File">The one file the edit may touch; anything else stops the run.</param>
/// <param name="Old">A literal that must appear exactly once.</param>
/// <param name="New">What replaces it.</param>
/// <param name="Class">The test class to run, or null for the set's own filter.</param>
/// <param name="Expect">`red` or `green`, compared rather than printed.</param>
/// <param name="Because">Why that is the right expectation — the part a reader needs in a year.</param>
/// <param name="Killer">For a red mutation, the test that must be among the failures.</param>
/// <param name="Says">Text the run must contain: the declared assertion's own words.</param>
internal sealed record Mutation(
    string Name, string File, string Old, string New, string? Class, string Expect, string? Because,
    string? Killer = null, string? Says = null)
{
    public Declared Declared => new(Expect, Killer, Says);

    /// <summary>Whether the edit lands in a file restore reads, so that the mutation's build must restore again.</summary>
    public bool EditsTheBuild =>
        File.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        || File.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
        || File.EndsWith(".targets", StringComparison.OrdinalIgnoreCase)
        || File.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
        || File.EndsWith("global.json", StringComparison.OrdinalIgnoreCase)
        || File.EndsWith("NuGet.config", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The test classes the mutation's run is filtered to, given the classes of its set's test files; none
    /// is the set's whole filter. Its own class, when it names one. A red mutation naming its killer runs
    /// only the set's classes, where that killer is declared — the trim the header measures. A green one,
    /// and a red one naming none, run everything their set would, so that narrowing can relabel a failure
    /// and never pass one.
    /// </summary>
    public IReadOnlyList<string> ClassesFrom(IReadOnlyList<string> setClasses) =>
        Class is { } named ? [named] : Expect == "red" && Killer is not null ? setClasses : [];

    /// <summary>What the mutation's run builds and tests, as <c>--list</c> prints it.</summary>
    public string Runs(IReadOnlyList<string> setClasses)
    {
        IReadOnlyList<string> classes = ClassesFrom(setClasses);
        return (classes.Count > 0 ? string.Join(", ", classes) : "the whole project")
            + (EditsTheBuild ? ", restored before it builds, the edit being to what restore reads" : ", built without a restore");
    }

    public string Describe()
    {
        string expectation = Killer is null ? Expect : $"{Expect}, killed by {Killer}";
        string reason = Because ?? (Says is null ? "the test it names guards it" : $"the test it names says \"{Says}\"");
        return $"{expectation} — {reason}";
    }
}

/// <summary>Which project the mutations are judged in, and what holds them to account.</summary>
/// <param name="Project">The test project, relative to the repository.</param>
/// <param name="Class">A test class every run is filtered to, or null for the whole project.</param>
/// <param name="Tests">Test files whose every test some mutation must kill; empty to judge none.</param>
/// <param name="Mutations">The mutations.</param>
/// <param name="Pins">The props file the tests' inert-at-pin markers are judged against, relative to the repository.</param>
internal sealed record Plan(string Project, string? Class, IReadOnlyList<string> Tests, IReadOnlyList<Mutation> Mutations, string Pins)
{
    /// <summary>The pins a set is judged against when it names none: the ones the build itself restores.</summary>
    public const string DefaultPins = "Directory.Packages.props";

    public static Plan BuiltIn { get; } = new(LibrarySet.Project, LibrarySet.EditUnderFold, [], LibrarySet.All, DefaultPins);

    public Declarations ReadTests(string repo)
    {
        List<DeclaredTest> tests = [];
        List<string> strays = [];
        List<string> barren = [];
        foreach (string file in Tests)
        {
            (IReadOnlyList<DeclaredTest> found, IReadOnlyList<int> stray) =
                Setup.TestsIn(File.ReadAllText(Path.Combine(repo, file.Replace('/', Path.DirectorySeparatorChar))));
            tests.AddRange(found);
            strays.AddRange(stray.Select(line => $"{file}:{line.ToString(CultureInfo.InvariantCulture)}"));
            if (found.Count == 0)
            {
                barren.Add(file);
            }
        }

        return new Declarations(tests, strays, barren);
    }

    /// <summary>The classes the set's test files declare, named as the test runner's class filter names them.</summary>
    public IReadOnlyList<string> ReadClasses(string repo) =>
        [.. Tests.SelectMany(t => Setup.ClassesIn(File.ReadAllText(Path.Combine(repo, t.Replace('/', Path.DirectorySeparatorChar))))).Distinct(StringComparer.Ordinal)];

    /// <summary>Reads a committed set, refusing one that leaves a mutation unable to be judged.</summary>
    public static Plan Load(string repo, string file)
    {
        string path = Path.IsPathRooted(file) ? file : Path.Combine(repo, file);
        MutationSet set = JsonSerializer.Deserialize(File.ReadAllText(path), MutationSetJson.Default.MutationSet)
            ?? throw new InvalidDataException("the set is empty");

        if (string.IsNullOrWhiteSpace(set.Project))
        {
            throw new InvalidDataException("the set names no project");
        }

        List<Mutation> mutations = [];
        foreach (SetMutation m in set.Mutations ?? [])
        {
            string expect = m.Expect ?? "red";
            if (string.IsNullOrWhiteSpace(m.Name) || string.IsNullOrWhiteSpace(m.File) || string.IsNullOrEmpty(m.Old) || m.New is null)
            {
                throw new InvalidDataException($"a mutation lacks a name, a file, an old text or a new one: \"{m.Name}\"");
            }

            if (expect is not ("red" or "green"))
            {
                throw new InvalidDataException($"\"{m.Name}\" expects `{expect}`, which is neither red nor green");
            }

            // A mutation with no stated reason is one nobody can judge a year from now. For a red one
            // the killer it names is a reason — test names here are sentences — and a green one must
            // say why the change should not matter.
            if (m.Killer is null && m.Because is null)
            {
                throw new InvalidDataException($"\"{m.Name}\" names neither its killer nor why it expects what it does");
            }

            if (expect == "green" && (m.Killer is not null || m.Says is not null))
            {
                throw new InvalidDataException($"\"{m.Name}\" is declared green and names a killer or words to say, which only a red one can");
            }

            mutations.Add(new Mutation(m.Name, m.File, m.Old, m.New, m.Class ?? set.Class, expect, m.Because, m.Killer, m.Says));
        }

        return mutations.Count == 0
            ? throw new InvalidDataException("the set declares no mutation")
            : new Plan(set.Project, set.Class, set.Tests ?? [], mutations, set.Pins ?? DefaultPins);
    }
}

internal sealed record MutationSet(string Project, string? Class, List<string>? Tests, string? Pins, List<SetMutation>? Mutations);

internal sealed record SetMutation(
    string Name, string File, string Old, string New, string? Expect, string? Killer, string? Says, string? Because, string? Class);

// ⛔ A key the set's types do not know is REFUSED, not skipped: `"kiler"` read as nothing would drop
// the one check a mutation was written to make, and the run would report the mutation as declared.
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(MutationSet))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
partial class MutationSetJson : JsonSerializerContext;

/// <summary>The built-in set: the library's edit-under-fold scenario, run when no --set is named.</summary>
internal static class LibrarySet
{
    public const string Project = "tests/DiffView.Avalonia.Tests/DiffView.Avalonia.Tests.csproj";

    public const string EditUnderFold = "Bennewitz.Ninja.DiffView.Tests.Composite.EditUnderFoldTests";

    public static readonly IReadOnlyList<Mutation> All =
    [
        new(
            "the re-diff stops refolding",
            "src/DiffView.Avalonia/DiffBuildController.cs",
            "        // And the runs are new, so they are computed again for this model rather than carried.\n        RefreshFolds();",
            "        // MUTATION: the re-diff no longer refolds.\n        ////RefreshFolds();",
            EditUnderFold,
            "red",
            "the folds after a revert can only come back from the re-diff's RefreshFolds, so the "
            + "post-build state reddens while the pre-build one stays green — that discrimination is "
            + "what proves the test's wait did not swallow the assertion"),

        new(
            "the revert leaves the text alone",
            "src/DiffView.Avalonia/SideBySideDiffView.cs",
            "            document.Text = source.Text;",
            "            ////document.Text = source.Text;   // MUTATION",
            EditUnderFold,
            "red",
            "with the text never restored the fold's lines are still the edited ones, so the middle "
            + "state — nothing collapsed the moment the revert returns — is what fails"),

        new(
            "a runner that loses the race",
            "tests/DiffView.Avalonia.Tests/Composite/CompositeHost.cs",
            "        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, token, options);",
            "        Thread.Sleep(50);   // MUTATION\n        DiffBuildResult result = DiffDocumentBuilder.Build(left, right, token, options);",
            EditUnderFold,
            "green",
            "⭐ the whole point. A build that takes 50 ms is what a slower CI runner is, and it is what "
            + "reddened this test intermittently on macOS before the repair: the same sleep fails the "
            + "OLD test with CI's numbers character for character, 17.554062499999997 against "
            + "351.08124999999995. Staying green here is the proof that the test now waits rather than "
            + "races, and a red means the wait has come undone"),
    ];
}

/// <summary>A checkout's state as a git tree: what --parallel gives each worktree, and what each shard says it ran over.</summary>
internal static class Source
{
    /// <summary>
    /// Where untracked files are carried from: what a set can build, read or edit. A tracked change is
    /// carried from anywhere; an untracked file elsewhere — a draft beside the plans — never leaves here.
    /// </summary>
    private static readonly string[] Scope =
        ["src", "tests", "scripts", "fixtures", "Directory.Build.props", "Directory.Packages.props", "global.json", "NuGet.config", "DiffView.slnx"];

    /// <summary>
    /// The tree this checkout's working state would commit as — HEAD, every tracked change, and each
    /// untracked file under <see cref="Scope"/> that the ignore rules let through — written through a
    /// temporary index, so that this checkout's own index and every ref stay where they are and no commit
    /// is made. Equal trees are equal states, which is how a worktree is shown to be this checkout.
    /// </summary>
    public static string Tree(string repo)
    {
        string directory = Path.Combine(Path.GetTempPath(), "mutate-behaviour");
        Directory.CreateDirectory(directory);
        string index = Path.Combine(directory, $"index-{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}");
        Dictionary<string, string> environment = new(StringComparer.Ordinal) { ["GIT_INDEX_FILE"] = index };
        try
        {
            Git(repo, environment, ["read-tree", "HEAD"]);
            Git(repo, environment, ["add", "--update"]);
            Git(repo, environment, ["add", "--", .. Scope.Where(p => Path.Exists(Path.Combine(repo, p)))]);
            return Git(repo, environment, ["write-tree"]).Trim();
        }
        finally
        {
            File.Delete(index);
        }
    }

    /// <summary>Gives a worktree cut from HEAD the state <paramref name="tree"/> holds, in its index and its files.</summary>
    public static void Load(string worktree, string tree) => Git(worktree, null, ["read-tree", "--reset", "-u", tree]);

    private static string Git(string repo, IReadOnlyDictionary<string, string>? environment, string[] arguments)
    {
        Shell.Result result = Shell.Run(repo, "git", arguments, environment: environment);
        return result.Exit == 0
            ? result.Stdout
            : throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed in {repo}: {result.Output.Trim()}");
    }
}

/// <summary>A set's mutations dealt into shards, a shard's verdicts written down, and shards read back as one run.</summary>
internal static class Shards
{
    /// <summary>
    /// The kth of n shards: every nth mutation from the kth. Dealt round rather than cut into runs, so the
    /// slow mutations of a set, which sit together, spread across the shards.
    /// </summary>
    public static IEnumerable<Mutation> Of(IReadOnlyList<Mutation> all, int index, int count) =>
        all.Where((_, position) => position % count == index - 1);

    /// <summary>How a set is named in a shard's result: as it was given, with <c>/</c>.</summary>
    public static string Name(string? set) => set is null ? "(built in)" : set.Replace('\\', '/');

    /// <summary>
    /// One run's verdicts as tab-separated lines — the shard it was, the tree it ran over and its set, then
    /// a line per verdict, per test each mutation failed and per first assertion it said. Plain text, so two
    /// runs compare by sorting and diffing. Nothing is written when <paramref name="path"/> is null.
    /// </summary>
    public static void Emit(
        string? path,
        string label,
        string? tree,
        string? set,
        IReadOnlyList<Mutation> chosen,
        IReadOnlyDictionary<string, (Reading Reading, string Outcome)> results)
    {
        if (path is null)
        {
            return;
        }

        List<string> lines = [$"shard\t{label}\t{tree}\t{Name(set)}"];
        foreach (Mutation m in chosen)
        {
            if (!results.TryGetValue(m.Name, out (Reading Reading, string Outcome) r))
            {
                continue;
            }

            lines.Add($"mutation\t{m.Name}\t{r.Outcome}");
            lines.AddRange(r.Reading.Failed.Select(test => $"killed\t{m.Name}\t{test}"));
            if (r.Reading.Message.Length > 0)
            {
                lines.Add($"said\t{m.Name}\t{r.Reading.Message.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ')}");
            }
        }

        File.WriteAllLines(path, lines);
    }

    /// <summary>
    /// One set's shards read back as one run's results, in the declared order. ⛔ Answers why it refuses
    /// unless the files are shards k/n of that set for every k of one n, over one tree, naming every
    /// declared mutation exactly once between them: a merge of a partial set would judge only what it
    /// reached and present that as the whole, so a lost shard must fail the run rather than shrink it.
    /// </summary>
    public static string? Read(
        IReadOnlyList<string> files,
        string? set,
        IReadOnlyList<Mutation> declared,
        Dictionary<string, (Reading Reading, string Outcome)> results,
        out string tree)
    {
        tree = string.Empty;
        int? total = null;
        HashSet<int> shards = [];
        Dictionary<string, string> outcomes = new(StringComparer.Ordinal);
        Dictionary<string, List<string>> kills = new(StringComparer.Ordinal);
        Dictionary<string, string> said = new(StringComparer.Ordinal);
        foreach (string file in files)
        {
            if (!File.Exists(file))
            {
                return $"no shard's result at {file}";
            }

            string[] lines = File.ReadAllLines(file);
            string[] header = lines.Length > 0 ? lines[0].Split('\t') : [];
            string[] place = header.Length == 4 && header[0] == "shard" ? header[1].Split('/') : [];
            if (place.Length != 2
                || !int.TryParse(place[0], NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || !int.TryParse(place[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count))
            {
                return $"{file} is not a shard's result: its first line names no shard k/n";
            }

            if (header[3] != Name(set))
            {
                return $"{file} is a result for {header[3]}, not for {Name(set)}";
            }

            if (total is { } n && n != count)
            {
                return $"{file} is a shard of {count.ToString(CultureInfo.InvariantCulture)}, where the others are shards of {n.ToString(CultureInfo.InvariantCulture)}";
            }

            if (tree.Length > 0 && header[2] != tree)
            {
                return $"{file} ran over tree {header[2]}, where the others ran over {tree}";
            }

            if (!shards.Add(index))
            {
                return $"shard {index.ToString(CultureInfo.InvariantCulture)}/{count.ToString(CultureInfo.InvariantCulture)} is given twice";
            }

            (total, tree) = (count, header[2]);
            foreach (string line in lines.Skip(1))
            {
                switch (line.Split('\t'))
                {
                    case ["mutation", string name, string outcome]:
                        if (!outcomes.TryAdd(name, outcome))
                        {
                            return $"\"{name}\" is reported by two shards";
                        }

                        break;

                    case ["killed", string name, string test]:
                        if (!kills.TryGetValue(name, out List<string>? tests))
                        {
                            kills[name] = tests = [];
                        }

                        tests.Add(test);
                        break;

                    case ["said", string name, string words]:
                        said[name] = words;
                        break;

                    default:
                        return $"{file} holds a line no run writes: '{line}'";
                }
            }
        }

        if (total is not { } whole || shards.Count != whole || shards.Any(s => s < 1 || s > whole))
        {
            return $"the results are {shards.Count.ToString(CultureInfo.InvariantCulture)} shard(s) of "
                + $"{total?.ToString(CultureInfo.InvariantCulture) ?? "none"}, and every shard must report";
        }

        string[] missing = [.. declared.Select(m => m.Name).Where(name => !outcomes.ContainsKey(name))];
        string[] unknown = [.. outcomes.Keys.Where(name => declared.All(m => m.Name != name))];
        if (missing.Length > 0 || unknown.Length > 0)
        {
            return $"the shards report {outcomes.Count.ToString(CultureInfo.InvariantCulture)} mutation(s) where "
                + $"{declared.Count.ToString(CultureInfo.InvariantCulture)} are declared"
                + (missing.Length > 0 ? $"; missing: {string.Join(", ", missing)}" : string.Empty)
                + (unknown.Length > 0 ? $"; not declared: {string.Join(", ", unknown)}" : string.Empty);
        }

        string[] strangers = [.. kills.Keys.Concat(said.Keys).Where(name => !outcomes.ContainsKey(name)).Distinct(StringComparer.Ordinal)];
        if (strangers.Length > 0)
        {
            return $"kills or words are credited to mutations no shard reports: {string.Join(", ", strangers)}";
        }

        foreach (Mutation m in declared)
        {
            IReadOnlyList<string> failed = kills.TryGetValue(m.Name, out List<string>? tests) ? tests : [];
            results[m.Name] = (new Reading(string.Empty, said.GetValueOrDefault(m.Name) ?? string.Empty, [], failed), outcomes[m.Name]);
        }

        return null;
    }

    /// <summary>
    /// A set's shards judged as one run of it, exactly as a run judges itself — or refused, answering 2,
    /// when they are not every mutation exactly once or ran over a tree other than <paramref name="expectedTree"/>.
    /// </summary>
    public static int JudgeShards(string? set, Plan plan, Declarations declared, IReadOnlyList<string> files, string expectedTree)
    {
        Dictionary<string, (Reading Reading, string Outcome)> results = new(StringComparer.Ordinal);
        if (Read(files, set, plan.Mutations, results, out string tree) is { } refusal)
        {
            Console.Error.WriteLine($"mutate-behaviour: {refusal}");
            return 2;
        }

        if (tree != expectedTree)
        {
            Console.Error.WriteLine($"mutate-behaviour: the shards ran over tree {tree}, and this checkout is tree {expectedTree}, so their verdicts are about other code");
            return 2;
        }

        Dictionary<string, string> killedBy = new(StringComparer.Ordinal);
        foreach (Mutation m in plan.Mutations)
        {
            Verdict.RecordKills(killedBy, m.Name, results[m.Name].Reading.Failed);
        }

        Console.WriteLine($"merged {files.Count.ToString(CultureInfo.InvariantCulture)} shard result(s) over tree {tree}");
        return Verdict.Report(plan.Mutations, results, declared, killedBy, narrowed: false, restoresHeld: true);
    }
}

/// <summary>
/// --parallel: each set's mutations dealt into shards, run at once in worktrees, and each set judged as one
/// run. This checkout is read, never edited.
/// </summary>
internal static partial class Parallel
{
    public static int Run(string repo, IReadOnlyList<(string? File, Plan Plan, Declarations Declared)> sets, int count)
    {
        string tree = Source.Tree(repo);
        string root = Path.Combine(Path.GetTempPath(), $"mutate-behaviour-parallel-{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}");
        string[] trees = [.. Enumerable.Range(1, count).Select(k => Path.Combine(root, k.ToString(CultureInfo.InvariantCulture)))];
        string Result(int s, int k) => Path.Combine(root, $"set-{(s + 1).ToString(CultureInfo.InvariantCulture)}-shard-{(k + 1).ToString(CultureInfo.InvariantCulture)}.tsv");
        string Log(int s, int k) => Path.ChangeExtension(Result(s, k), ".log");

        (string self, string[] launcher) = Self.Binary();
        int total = sets.Sum(s => s.Plan.Mutations.Count);
        Console.WriteLine($"--parallel {count.ToString(CultureInfo.InvariantCulture)}: {total.ToString(CultureInfo.InvariantCulture)} mutation(s) in "
            + $"{sets.Count.ToString(CultureInfo.InvariantCulture)} set(s), over this checkout as tree {tree}, in worktrees under {root}");
        Console.WriteLine("  if this is killed, remove what it leaves with git worktree remove --force <dir>, then git worktree prune");

        Process?[] running = new Process?[count];
        int[] onSet = new int[count];
        int[] done = new int[count];
        int[,] codes = new int[sets.Count, count];
        bool interrupted = false;
        void Stop(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Volatile.Write(ref interrupted, true);
        }

        Console.CancelKeyPress += Stop;
        Stopwatch clock = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(root);
            for (int k = 0; k < count; k++)
            {
                Shell.Result cut = Shell.Run(repo, "git", ["worktree", "add", "--detach", trees[k], "HEAD"]);
                if (cut.Exit != 0)
                {
                    Console.Error.WriteLine($"mutate-behaviour: could not cut worktree {(k + 1).ToString(CultureInfo.InvariantCulture)}: {cut.Output.Trim()}");
                    return 2;
                }

                Source.Load(trees[k], tree);

                // ⛔ The proof that the worktree is this checkout, before a minute is spent on it.
                if (Source.Tree(trees[k]) is var loaded && loaded != tree)
                {
                    Console.Error.WriteLine($"mutate-behaviour: worktree {(k + 1).ToString(CultureInfo.InvariantCulture)} holds tree {loaded}, not this checkout's {tree}");
                    return 2;
                }
            }

            Task[] workers = [.. Enumerable.Range(0, count).Select(k => Task.Run(() =>
            {
                for (int s = 0; s < sets.Count && !Volatile.Read(ref interrupted); s++)
                {
                    Volatile.Write(ref onSet[k], s);
                    Volatile.Write(ref done[k], 0);
                    ProcessStartInfo info = new(self)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = trees[k],
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };

                    string[] setArgument = sets[s].File is { } file ? ["--set", file] : [];
                    foreach (string argument in (string[])[.. launcher, .. setArgument, "--shard", $"{(k + 1).ToString(CultureInfo.InvariantCulture)}/{count.ToString(CultureInfo.InvariantCulture)}", "--emit", Result(s, k)])
                    {
                        info.ArgumentList.Add(argument);
                    }

                    // No set reads the reference checkouts, and fetching them into a worktree clones every one.
                    info.Environment["SkipReferenceSources"] = "true";

                    using StreamWriter writer = new(Log(s, k)) { AutoFlush = true };
                    using Process process = new() { StartInfo = info };
                    int worker = k;
                    void Record(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data is not { } line)
                        {
                            return;
                        }

                        lock (writer)
                        {
                            writer.WriteLine(line);
                        }

                        if (Progress().IsMatch(line))
                        {
                            Interlocked.Increment(ref done[worker]);
                        }
                    }

                    process.OutputDataReceived += Record;
                    process.ErrorDataReceived += Record;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    Volatile.Write(ref running[k], process);
                    process.WaitForExit();
                    codes[s, k] = process.ExitCode;
                    Volatile.Write(ref running[k], null);
                }
            }))];

            // ⭐ Prints as it goes, a line every half minute, so a long run reads as moving or stopped.
            TimeSpan printed = TimeSpan.Zero;
            while (!Task.WaitAll(workers, 2000))
            {
                if (Volatile.Read(ref interrupted))
                {
                    Console.Error.WriteLine("mutate-behaviour: interrupted, so the shards are stopped and their worktrees removed");
                    return 2;
                }

                if (clock.Elapsed - printed >= TimeSpan.FromSeconds(30))
                {
                    printed = clock.Elapsed;
                    Console.WriteLine($"  {Minutes(clock.Elapsed)}   " + string.Join("   ", Enumerable.Range(0, count).Select(k =>
                    {
                        int s = Volatile.Read(ref onSet[k]);
                        int size = Shards.Of(sets[s].Plan.Mutations, k + 1, count).Count();
                        return $"shard {(k + 1).ToString(CultureInfo.InvariantCulture)}: set {(s + 1).ToString(CultureInfo.InvariantCulture)}/"
                            + $"{sets.Count.ToString(CultureInfo.InvariantCulture)} {Volatile.Read(ref done[k]).ToString(CultureInfo.InvariantCulture)}/{size.ToString(CultureInfo.InvariantCulture)}";
                    })));
                }
            }

            Console.WriteLine($"all {count.ToString(CultureInfo.InvariantCulture)} shard(s) finished in {Minutes(clock.Elapsed)}");

            int worst = 0;
            for (int s = 0; s < sets.Count; s++)
            {
                Console.WriteLine();
                Console.WriteLine($"=== {Shards.Name(sets[s].File)}");
                bool usable = true;
                for (int k = 0; k < count; k++)
                {
                    // 0 and 1 are both results — a shard judges its own share's verdicts and never the set's
                    // tests — and the merge reads either. Anything else is the harness refusing or failing.
                    if (codes[s, k] is not (0 or 1) || !File.Exists(Result(s, k)))
                    {
                        usable = false;
                        Console.Error.WriteLine($"shard {(k + 1).ToString(CultureInfo.InvariantCulture)}/{count.ToString(CultureInfo.InvariantCulture)}: "
                            + $"exit {codes[s, k].ToString(CultureInfo.InvariantCulture)}, NO RESULT; its log is {Log(s, k)}");
                    }
                }

                int code = usable
                    ? Shards.JudgeShards(sets[s].File, sets[s].Plan, sets[s].Declared, [.. Enumerable.Range(0, count).Select(k => Result(s, k))], tree)
                    : 2;
                Console.WriteLine($"=== exit {code.ToString(CultureInfo.InvariantCulture)}");
                worst = Math.Max(worst, code);
            }

            Console.WriteLine();
            Console.WriteLine($"{sets.Count.ToString(CultureInfo.InvariantCulture)} set(s) in {count.ToString(CultureInfo.InvariantCulture)} worktree(s), "
                + $"{Minutes(clock.Elapsed)}; each shard's log and result are kept in {root}");
            return worst;
        }
        finally
        {
            Console.CancelKeyPress -= Stop;
            foreach (Process? process in running)
            {
                try
                {
                    if (process is { HasExited: false })
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Exited and disposed between the read and the kill.
                }
            }

            string[] left = [.. trees.Where(Directory.Exists).Where(t => !RemoveWorktree(repo, t))];
            Shell.Run(repo, "git", ["worktree", "prune"]);
            if (left.Length > 0)
            {
                Console.Error.WriteLine($"mutate-behaviour: {left.Length.ToString(CultureInfo.InvariantCulture)} worktree(s) would not delete, and are left:");
                foreach (string t in left)
                {
                    Console.Error.WriteLine("  " + t);
                }

                Console.Error.WriteLine("Something still holds a file in each. Delete them once it lets go, then run git worktree prune.");
            }
        }
    }

    private static string Minutes(TimeSpan elapsed) => elapsed.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture) + " min";

    /// <summary>
    /// A build that has just finished can hold a file in its worktree a little longer — measured by
    /// <c>scripts/mutate-gates</c>, whose retries these are — so this tries for about a minute, deleting the
    /// directory itself once git has given up on it.
    /// </summary>
    private static bool RemoveWorktree(string repo, string tree)
    {
        Shell.Run(repo, "git", ["worktree", "remove", "--force", tree]);
        foreach (int seconds in (int[])[2, 5, 10, 20, 30])
        {
            if (!Directory.Exists(tree))
            {
                return true;
            }

            Thread.Sleep(TimeSpan.FromSeconds(seconds));
            try
            {
                Directory.Delete(tree, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still held; the next pass waits longer.
            }
        }

        return !Directory.Exists(tree);
    }

    /// <summary>A shard's progress line, one per mutation it has run.</summary>
    [GeneratedRegex(@"^\[\d+/\d+\]")]
    private static partial Regex Progress();
}

/// <summary>This harness again, as the binary running now — so that no child rebuilds the script it runs from.</summary>
internal static class Self
{
    /// <summary>
    /// The binary to start and what to hand it first. Under the dotnet host that is the entry assembly beside
    /// <see cref="AppContext.BaseDirectory"/>; as an apphost, the apphost itself.
    /// </summary>
    public static (string File, string[] Launcher) Binary()
    {
        string self = Environment.ProcessPath ?? "dotnet";
        string[] launcher = string.Equals(Path.GetFileNameWithoutExtension(self), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? [Path.Combine(AppContext.BaseDirectory, System.Reflection.Assembly.GetEntryAssembly()!.GetName().Name + ".dll")]
            : [];
        return (self, launcher);
    }

    /// <summary>
    /// Several sets one after another in this checkout, each a run of this harness judged on its own; answers
    /// 2 when any set was refused, 1 when any was not as declared, else 0.
    /// </summary>
    public static int EachSet(string repo, IReadOnlyList<string> sets, IReadOnlyList<string> passed)
    {
        (string self, string[] launcher) = Binary();
        List<int> codes = [];
        foreach (string set in sets)
        {
            Console.WriteLine($"=== {set}");
            ProcessStartInfo start = new(self) { UseShellExecute = false, WorkingDirectory = repo };
            foreach (string argument in (string[])[.. launcher, "--set", set, .. passed])
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start) ?? throw new InvalidOperationException("could not start this harness again");
            process.WaitForExit();
            codes.Add(process.ExitCode);
            Console.WriteLine($"=== exit {process.ExitCode.ToString(CultureInfo.InvariantCulture)}");
        }

        int failed = codes.Count(c => c != 0);
        Console.WriteLine($"{codes.Count.ToString(CultureInfo.InvariantCulture)} set(s), {failed.ToString(CultureInfo.InvariantCulture)} not as declared");
        return codes.Contains(2) ? 2 : failed > 0 ? 1 : 0;
    }
}

/// <summary>Which committed sets a change owes a run.</summary>
internal static class Owed
{
    /// <summary>Every set committed beside its tests, <c>tests/&lt;project&gt;/Mutations/*.json</c>, named relative to the repository.</summary>
    public static IEnumerable<string> CommittedSets(string repo)
    {
        foreach (string project in Directory.EnumerateDirectories(Path.Combine(repo, "tests")).Order(StringComparer.Ordinal))
        {
            string mutations = Path.Combine(project, "Mutations");
            if (!Directory.Exists(mutations))
            {
                continue;
            }

            foreach (string set in Directory.EnumerateFiles(mutations, "*.json").Order(StringComparer.Ordinal))
            {
                yield return Path.GetRelativePath(repo, set).Replace('\\', '/');
            }
        }
    }

    /// <summary>
    /// The first of a set's inputs a change touched — the set's own file, its pins, one of its test files, a
    /// file one of its mutations edits — or null when the change owes it nothing.
    /// </summary>
    public static string? Because(string repo, string setFile, Plan plan, IReadOnlyList<string> changed)
    {
        StringComparer paths = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        string Normal(string path) =>
            Path.GetRelativePath(repo, Path.GetFullPath(Path.Combine(repo, path.Trim().Replace('/', Path.DirectorySeparatorChar)))).Replace('\\', '/');

        HashSet<string> touched = new(changed.Select(Normal), paths);
        string[] inputs = [setFile, plan.Pins, .. plan.Tests, .. plan.Mutations.Select(m => m.File)];
        return inputs.Select(Normal).FirstOrDefault(touched.Contains);
    }
}
