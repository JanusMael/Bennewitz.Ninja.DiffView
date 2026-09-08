using System.Text;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Core.Tests;

/// <summary>
/// Plan 00003 §Phase 3, the save round-trip. A file that is read, edited on one line and written
/// back must differ from the original in exactly that line's bytes — so the encoding, the
/// byte-order mark and the line terminators all have to survive the trip.
/// </summary>
public sealed class PaneWriterTests
{
    [Theory]
    [InlineData(LineEnding.Lf, "a\nb\nc")]
    [InlineData(LineEnding.CrLf, "a\r\nb\r\nc")]
    [InlineData(LineEnding.Cr, "a\rb\rc")]
    public void Normalize_rewrites_every_terminator_to_the_convention(LineEnding ending, string expected)
    {
        // One of each on the way in; the pair must count as one terminator, not two.
        Assert.Equal(expected, PaneWriter.Normalize("a\r\nb\nc", ending));
        Assert.Equal(expected, PaneWriter.Normalize("a\rb\r\nc", ending));
        Assert.Equal(expected, PaneWriter.Normalize(expected, ending));
    }

    [Theory]
    [InlineData(LineEnding.None)]
    [InlineData(LineEnding.Mixed)]
    public void Normalize_leaves_a_text_with_no_single_convention_alone(LineEnding ending)
    {
        const string mixed = "a\r\nb\nc\rd";
        Assert.Equal(mixed, PaneWriter.Normalize(mixed, ending));
    }

    [Fact]
    public void A_utf8_file_with_a_mark_round_trips_byte_for_byte()
    {
        byte[] original = Bytes(Encoding.UTF8, "using System;\r\nclass C { }\r\n");
        PaneSource source = PaneSource.FromBytes(original);

        Assert.Equal("using System;\r\nclass C { }\r\n", source.Text);
        byte[] written = PaneWriter.ToBytes(source.Text, source.Encoding, LineEnding.CrLf);
        Assert.Equal(original, written);

        // The mark is the first three bytes and is written exactly once.
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, written.AsSpan(0, 3).ToArray());
    }

    [Fact]
    public void A_utf8_file_without_a_mark_gains_none()
    {
        byte[] original = Encoding.UTF8.GetBytes("plain\nlines\n");
        PaneSource source = PaneSource.FromBytes(original);

        byte[] written = PaneWriter.ToBytes(source.Text, source.Encoding, LineEnding.Lf);
        Assert.Equal(original, written);
        Assert.NotEqual(0xEF, written[0]);
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    public void A_utf16_or_utf32_file_keeps_its_encoding_and_its_mark(string webName)
    {
        Encoding encoding = Encoding.GetEncoding(webName);
        byte[] original = Bytes(encoding, "héllo\nwörld\n");
        PaneSource source = PaneSource.FromBytes(original);

        Assert.Equal("héllo\nwörld\n", source.Text);
        Assert.Equal(original, PaneWriter.ToBytes(source.Text, source.Encoding, LineEnding.Lf));
    }

    [Fact]
    public void A_latin1_file_round_trips_through_the_fallback()
    {
        // 0xFF is not valid UTF-8, so strict decoding fails and Latin-1 takes over.
        byte[] original = [(byte)'a', 0xFF, (byte)'\n', (byte)'b', (byte)'\n'];
        PaneSource source = PaneSource.FromBytes(original);

        Assert.True(source.Latin1Fallback);
        Assert.Equal(original, PaneWriter.ToBytes(source.Text, source.Encoding, LineEnding.Lf));
    }

    [Fact]
    public void An_edit_to_one_line_changes_only_that_line_bytes()
    {
        byte[] original = Bytes(Encoding.UTF8, "alpha\r\nbeta\r\ngamma\r\n");
        PaneSource source = PaneSource.FromBytes(original);

        // The editor holds LF internally; the file was CRLF and must stay CRLF.
        string edited = source.Text.Replace("\r\n", "\n", StringComparison.Ordinal)
                                   .Replace("beta", "BETA", StringComparison.Ordinal);
        byte[] written = PaneWriter.ToBytes(edited, source.Encoding, LineEnding.CrLf);

        Assert.Equal(Bytes(Encoding.UTF8, "alpha\r\nBETA\r\ngamma\r\n"), written);
        Assert.Equal(original.Length, written.Length);
    }

    [Fact]
    public void A_source_built_from_a_string_writes_utf8_with_no_mark()
    {
        PaneSource source = "no encoding was ever detected\n";
        Assert.Null(source.Encoding);

        byte[] written = PaneWriter.ToBytes(source.Text, source.Encoding, LineEnding.Lf);
        Assert.Equal(Encoding.UTF8.GetBytes(source.Text), written);
    }

    [Fact]
    public void Write_puts_the_bytes_on_disk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"diffview-panewriter-{Guid.NewGuid():N}.txt");
        try
        {
            PaneWriter.Write(path, "one\ntwo\n", Encoding.UTF8, LineEnding.CrLf);
            Assert.Equal(Bytes(Encoding.UTF8, "one\r\ntwo\r\n"), File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The encoding's own preamble followed by its encoding of the text.</summary>
    private static byte[] Bytes(Encoding encoding, string text)
    {
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }
}
