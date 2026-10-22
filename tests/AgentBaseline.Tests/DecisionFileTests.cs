using System.Text;

namespace Bennewitz.Ninja.AgentBaseline.Tests;

/// <summary>
/// Plan 00034 step 8's decision file: the durable store on disk, written as this tool writes it and read
/// only when it is.
/// </summary>
/// <remarks>
/// The shape is pinned as text, because the file outlives the build that wrote it: a later run reads what
/// an earlier one left, so a key renamed, a value written another way or an escape added is a file the
/// next version cannot read, or reads otherwise.
/// </remarks>
public sealed class DecisionFileTests
{
    private const string Rule = "## A rule\n\nBody — an em dash, ⛔ a sign, \"quotes\", a back\\slash & a | pipe.";

    private static SectionDecision Taken(string headingPath, string text, string source) =>
        new(DecisionKind.Taken, new SectionIdentity(headingPath, SectionIdentity.HashOf(text)), source, text);

    private static string Hash(string text) => SectionIdentity.HashOf(text);

    /// <summary>A file around <paramref name="decision"/>, a decision written by hand.</summary>
    private static string FileOf(string decision) => $$"""{ "version": 1, "decisions": [ {{decision}} ] }""";

    [Fact]
    public void Every_kind_of_decision_is_written_as_the_file_holds_it_and_read_back_as_it_was()
    {
        SectionDecision[] decisions =
        [
            Taken("Working preferences / A rule", Rule, "janus halo"),
            Taken("Two", "## Two\n\nBeta.", "machine 1") with { Edited = true, Result = "## Two\n\nBeta, edited." },
            Taken("Three", "## Three\n\nGamma.", "machine 1") with { Edited = true },
            new(DecisionKind.Rejected, new SectionIdentity("", Hash("# Rules")), "machine 2", null),
            new(DecisionKind.Dropped, new SectionIdentity("Four", Hash("## Four\n\nDelta.")), null, null),
        ];

        string written = DecisionFile.Format(decisions);

        // ⭐ A rule's own characters as they are, every structural line ending LF, and an edit made away
        // said as null rather than left out.
        Assert.Equal(
            $$"""
            {
              "version": 1,
              "decisions": [
                {
                  "kind": "taken",
                  "headingPath": "Working preferences / A rule",
                  "hash": "{{Hash(Rule)}}",
                  "source": "janus halo",
                  "text": "## A rule\n\nBody — an em dash, ⛔ a sign, \"quotes\", a back\\slash & a | pipe.",
                  "edited": false
                },
                {
                  "kind": "taken",
                  "headingPath": "Two",
                  "hash": "{{Hash("## Two\n\nBeta.")}}",
                  "source": "machine 1",
                  "text": "## Two\n\nBeta.",
                  "edited": true,
                  "result": "## Two\n\nBeta, edited."
                },
                {
                  "kind": "taken",
                  "headingPath": "Three",
                  "hash": "{{Hash("## Three\n\nGamma.")}}",
                  "source": "machine 1",
                  "text": "## Three\n\nGamma.",
                  "edited": true,
                  "result": null
                },
                {
                  "kind": "rejected",
                  "headingPath": "",
                  "hash": "{{Hash("# Rules")}}",
                  "source": "machine 2"
                },
                {
                  "kind": "dropped",
                  "headingPath": "Four",
                  "hash": "{{Hash("## Four\n\nDelta.")}}"
                }
              ]
            }

            """.ReplaceLineEndings("\n"),
            written);

        Assert.Equal(decisions, DecisionFile.Parse(written));
    }

    /// <summary>Files this tool did not write, each with the words its refusal must carry.</summary>
    public static TheoryData<string, string> Unwritten
    {
        get
        {
            string hash = Hash("## One\n\nAlpha.");
            string take = $$""" "kind": "taken", "headingPath": "One", "hash": "{{hash}}", "source": "m", "text": "## One\n\nAlpha." """;
            string leave = $$""" "kind": "rejected", "headingPath": "One", "hash": "{{hash}}", "source": "m" """;
            string drop = $$""" "kind": "dropped", "headingPath": "One", "hash": "{{hash}}" """;
            return new()
            {
                { "not json", "the decision file is not JSON" },
                { "[]", "the decision file is not a JSON object" },
                { """{ "version": 1, "decisions": [], "notes": "" }""", "the decision file has a key \"notes\" it does not know" },
                { """{ "decisions": [] }""", "the decision file says no \"version\"" },
                { """{ "version": 2, "decisions": [] }""", "the decision file is version 2, and this tool reads version 1 alone" },
                { """{ "version": "1", "decisions": [] }""", "the decision file is version \"1\"" },
                { """{ "version": 1 }""", "the decision file's \"decisions\" is not a list" },
                { FileOf("3"), "decision 1 is not a JSON object" },
                { FileOf($$"""{ {{leave}}, "weight": 1 }"""), "decision 1 has a key \"weight\" it does not know" },
                { FileOf($$"""{ "kind": "deferred", "headingPath": "One", "hash": "{{hash}}", "source": "m" }"""), "decision 1's \"kind\" is \"deferred\"" },
                { FileOf($$"""{ "kind": "taken", "headingPath": "One", "source": "m" }"""), "decision 1 says no \"hash\"" },
                { FileOf($$"""{ "kind": "rejected", "headingPath": "One", "hash": "{{hash.ToLowerInvariant()}}", "source": "m" }"""), "decision 1's \"hash\" is not a SHA-256 in upper-case hex" },
                { FileOf($$"""{ "kind": "rejected", "headingPath": "One", "hash": "{{hash[..63]}}", "source": "m" }"""), "decision 1's \"hash\" is not a SHA-256 in upper-case hex" },
                { FileOf($$"""{ "kind": "rejected", "headingPath": 1, "hash": "{{hash}}", "source": "m" }"""), "decision 1's \"headingPath\" is not a string" },
                { FileOf($$"""{ "kind": "rejected", "headingPath": "One", "hash": "{{hash}}" }"""), "decision 1 says no \"source\"" },
                { FileOf($$"""{ {{leave}}, "text": "## One\n\nAlpha." }"""), "decision 1 has the key \"text\": a leave carries no text" },
                { FileOf($$"""{ {{drop}}, "source": "m" }"""), "decision 1 has the key \"source\": a drop comes from no candidate" },
                { FileOf($$"""{ {{drop}}, "edited": false }"""), "decision 1 has the key \"edited\": a drop carries no text, and only a take is edited" },
                { FileOf($$"""{ "kind": "taken", "headingPath": "One", "hash": "{{hash}}", "source": "m", "text": "## One\n\nAlpha, changed.", "edited": false }"""), "decision 1's \"hash\" is not the hash of its \"text\"" },
                { FileOf($$"""{ {{take}} }"""), "decision 1 says no \"edited\"" },
                { FileOf($$"""{ {{take}}, "edited": "no" }"""), "decision 1's \"edited\" is not true or false" },
                { FileOf($$"""{ {{take}}, "edited": false, "result": null }"""), "decision 1 has the key \"result\": a take that was not edited has none" },
                { FileOf($$"""{ {{take}}, "edited": true }"""), "decision 1 was edited and says no \"result\"" },
                { FileOf($$"""{ {{take}}, "edited": true, "result": 3 }"""), "decision 1's \"result\" is not text or null" },
                { FileOf($$"""{ {{take}}, "edited": true, "result": "## One\n\nAlpha." }"""), "decision 1 says it was edited, and its \"result\" is the text it took" },
            };
        }
    }

    [Theory]
    [MemberData(nameof(Unwritten))]
    public void A_decision_file_this_tool_did_not_write_is_refused_and_says_why(string json, string because) =>
        Assert.Contains(
            because,
            Assert.Throws<DecisionFileRefusedException>(() => DecisionFile.Parse(json)).Message,
            StringComparison.Ordinal);

    [Fact]
    public void A_write_that_cannot_finish_leaves_the_file_it_would_replace_as_it_was()
    {
        using FixtureTree tree = new();
        string path = Path.Combine(tree.MakeDirectory("Baseline"), "AGENTS.decisions.json");
        SectionDecision[] before = [Taken("One", "## One\n\nAlpha.", "machine 1")];
        SectionDecision[] after = [.. before, Taken("Two", "## Two\n\nBeta.", "machine 1")];
        DecisionFile.Write(path, before);
        byte[] held = File.ReadAllBytes(path);

        // ⛔ Where the new file is written first stands a directory, so it cannot be: a write straight over
        // the file would succeed, and leave a store a later failure could have torn.
        Directory.CreateDirectory(path + ".partial");
        Exception refused = Assert.ThrowsAny<Exception>(() => DecisionFile.Write(path, after));
        Assert.True(refused is IOException or UnauthorizedAccessException, $"The write failed as {refused.GetType().Name}.");
        Assert.Equal(held, File.ReadAllBytes(path));
        Directory.Delete(path + ".partial");

        // A file that cannot be moved over — a directory where it would go — leaves no partial file behind.
        string blocked = Path.Combine(tree.MakeDirectory("Blocked"), "AGENTS.decisions.json");
        Directory.CreateDirectory(blocked);
        Exception moved = Assert.ThrowsAny<Exception>(() => DecisionFile.Write(blocked, after));
        Assert.True(moved is IOException or UnauthorizedAccessException, $"The move failed as {moved.GetType().Name}.");
        Assert.False(File.Exists(blocked + ".partial"), "A write that failed left its partial file behind.");

        // Unblocked, it replaces the file whole — LF, no byte-order mark — and leaves nothing beside it.
        DecisionFile.Write(path, after);
        Assert.Equal(after, DecisionFile.Read(path));
        Assert.Equal(Encoding.UTF8.GetBytes(DecisionFile.Format(after)), File.ReadAllBytes(path));
        Assert.Equal(["AGENTS.decisions.json"], Directory.GetFileSystemEntries(Path.GetDirectoryName(path)!).Select(Path.GetFileName));
    }
}
