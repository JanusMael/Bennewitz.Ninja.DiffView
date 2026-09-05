using System.Text;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

/// <summary>
/// The inputs the Core tests run on: the committed small pair under <c>fixtures/small</c>, and
/// large pairs generated deterministically from a seed — a 200k-line pair is megabytes that do
/// not belong in the repository, and a seeded generator reproduces it byte for byte.
/// </summary>
internal static class Fixtures
{
    private const string Marker = "DiffView.slnx";

    private static readonly string[] Words =
    [
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa",
        "lambda", "mu", "nu", "xi", "omicron", "pi", "rho", "sigma", "tau", "upsilon",
        "return", "value", "count", "index", "buffer", "offset", "length", "result", "item", "node",
    ];

    /// <summary>The repository root.</summary>
    public static string RepoRoot
    {
        get
        {
            string? directory = AppContext.BaseDirectory;
            for (int i = 0; i < 12 && !string.IsNullOrEmpty(directory); i++)
            {
                if (File.Exists(Path.Combine(directory, Marker)))
                {
                    return directory;
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new InvalidOperationException($"Could not find {Marker} above {AppContext.BaseDirectory}.");
        }
    }

    /// <summary>The committed small pair: a class with a using added, a field and parameter added, a method removed, a line changed.</summary>
    public static (string Left, string Right) Small()
    {
        string directory = Path.Combine(RepoRoot, "fixtures", "small");
        return (File.ReadAllText(Path.Combine(directory, "left.txt")), File.ReadAllText(Path.Combine(directory, "right.txt")));
    }

    /// <summary>
    /// <paramref name="count"/> code-like lines from <paramref name="seed"/>, joined with
    /// <paramref name="lineEnding"/> and ending with one.
    /// </summary>
    public static string Lines(int count, int seed, string lineEnding = "\n")
    {
        Random random = new(seed);
        StringBuilder sb = new(count * 40);
        for (int i = 0; i < count; i++)
        {
            AppendLine(sb, random, i);
            sb.Append(lineEnding);
        }

        return sb.ToString();
    }

    /// <summary>
    /// A pair that differs every <paramref name="changeEvery"/> lines: one line modified, one
    /// inserted after it, and one deleted a few lines later — so blocks of all three kinds occur.
    /// </summary>
    public static (string Left, string Right) SimilarPair(int lines, int seed, int changeEvery = 50)
    {
        Random random = new(seed);
        StringBuilder left = new(lines * 40);
        StringBuilder right = new(lines * 40);
        for (int i = 0; i < lines; i++)
        {
            StringBuilder line = new();
            AppendLine(line, random, i);
            string text = line.ToString();

            if (i % changeEvery == 10)
            {
                left.Append(text).Append('\n');
                right.Append(text.Replace(" = ", " := ", StringComparison.Ordinal)).Append('\n');
                right.Append("    // inserted after line ").Append(i).Append('\n');
            }
            else if (i % changeEvery == 20)
            {
                left.Append(text).Append('\n'); // deleted on the right
            }
            else
            {
                left.Append(text).Append('\n');
                right.Append(text).Append('\n');
            }
        }

        return (left.ToString(), right.ToString());
    }

    /// <summary>Two texts with nothing in common line for line: every line carries its side and a random token.</summary>
    public static (string Left, string Right) UnrelatedPair(int lines, int seed)
    {
        Random random = new(seed);
        StringBuilder left = new(lines * 32);
        StringBuilder right = new(lines * 32);
        for (int i = 0; i < lines; i++)
        {
            left.Append("L").Append(i).Append(' ').Append(Words[random.Next(Words.Length)]).Append(' ').Append(random.Next()).Append('\n');
            right.Append("R").Append(i).Append(' ').Append(Words[random.Next(Words.Length)]).Append(' ').Append(random.Next()).Append('\n');
        }

        return (left.ToString(), right.ToString());
    }

    /// <summary>One line of <paramref name="length"/> characters with no terminator.</summary>
    public static string SingleLongLine(int length, int seed = 7)
    {
        Random random = new(seed);
        StringBuilder sb = new(length);
        while (sb.Length < length)
        {
            sb.Append(Words[random.Next(Words.Length)]).Append(' ');
        }

        sb.Length = length;
        return sb.ToString();
    }

    /// <summary>Bytes that look like a binary file: a header, NULs early, then text.</summary>
    public static byte[] BinaryBytes()
    {
        List<byte> bytes = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        bytes.AddRange(Encoding.ASCII.GetBytes("IHDR and then some text so it is not all NULs\n"));
        return [.. bytes];
    }

    private static void AppendLine(StringBuilder sb, Random random, int index)
    {
        int indent = random.Next(4) * 4;
        sb.Append(' ', indent)
          .Append(Words[random.Next(Words.Length)]).Append(' ')
          .Append(Words[random.Next(Words.Length)]).Append(" = ")
          .Append(Words[random.Next(Words.Length)]).Append('(').Append(index).Append(", ")
          .Append(random.Next(1000)).Append(");");
    }
}
