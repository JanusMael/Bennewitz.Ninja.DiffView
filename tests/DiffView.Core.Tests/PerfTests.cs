using System.Diagnostics;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

/// <summary>
/// Stopwatch measurements the plan asks for; excluded from the default run by the
/// <c>Perf</c> trait and recorded in <c>PROGRESS.md</c>. Run with
/// <c>dotnet test --solution DiffView.slnx -p:IncludePerfTests=true</c>.
/// </summary>
[Trait("Category", "Perf")]
public sealed class PerfTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(10_000)]
    [InlineData(200_000)]
    public void Build_on_a_similar_pair(int lines)
    {
        (string left, string right) = Fixtures.SimilarPair(lines, seed: 11);

        DiffBuildResult warm = DiffDocumentBuilder.Build(left, right);
        Stopwatch stopwatch = Stopwatch.StartNew();
        DiffBuildResult result = DiffDocumentBuilder.Build(left, right);
        stopwatch.Stop();

        output.WriteLine($"Build {lines:N0} lines: {stopwatch.ElapsedMilliseconds} ms (engine-reported {result.Diagnostics.BuildTime.TotalMilliseconds:F0} ms; warm-up {warm.Diagnostics.BuildTime.TotalMilliseconds:F0} ms); {result.Diagnostics.RowCount:N0} rows, {result.Diagnostics.BlockCount:N0} blocks, similarity {result.Diagnostics.Similarity:F3}");
        Assert.True(result.Diagnostics.Aligned);
        Assert.True(result.Diagnostics.BlockCount > 0);
    }

    [Fact]
    public void The_similarity_gate_on_the_unrelated_pair()
    {
        (string left, string right) = Fixtures.UnrelatedPair(200_000, seed: 13);
        string[] leftLines = LineSplitter.Split(left);
        string[] rightLines = LineSplitter.Split(right);

        Stopwatch stopwatch = Stopwatch.StartNew();
        double similarity = SimilarityGate.Measure(leftLines, rightLines, DiffOptions.Default);
        stopwatch.Stop();
        output.WriteLine($"Similarity gate on 2 × 200,000 unrelated lines: {stopwatch.ElapsedMilliseconds} ms, similarity {similarity:F4}");

        stopwatch.Restart();
        DiffBuildResult result = DiffDocumentBuilder.Build(left, right);
        stopwatch.Stop();
        output.WriteLine($"Build (gated, unaligned) on the same pair: {stopwatch.ElapsedMilliseconds} ms");

        Assert.False(result.Diagnostics.Aligned);
        Assert.True(similarity < 0.01);
    }

    [Fact]
    public void Search_on_the_10k_pair()
    {
        (string left, string right) = Fixtures.SimilarPair(10_000, seed: 11);
        SideBySideDocument document = DiffDocumentBuilder.Build(left, right).Document;
        StringPaneText leftText = new(left);
        StringPaneText rightText = new(right);

        Stopwatch stopwatch = Stopwatch.StartNew();
        FindResult literal = DiffSearch.Find(document, leftText, rightText, "alpha");
        stopwatch.Stop();
        output.WriteLine($"Literal search over 10k pair: {stopwatch.ElapsedMilliseconds} ms, {literal.Matches.Count:N0} matches, truncated {literal.Truncated}");

        stopwatch.Restart();
        FindResult regex = DiffSearch.Find(document, leftText, rightText, @"\b(alpha|beta)\b", new FindOptions { UseRegex = true, MaxMatches = 100_000 });
        stopwatch.Stop();
        output.WriteLine($"Regex search over 10k pair: {stopwatch.ElapsedMilliseconds} ms, {regex.Matches.Count:N0} matches");

        Assert.Null(literal.Error);
        Assert.Null(regex.Error);
    }
}
