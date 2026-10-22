using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Bennewitz.Ninja.AgentBaseline;

/// <summary>
/// A decision file refused before a decision was taken from it: what it holds is not what this tool
/// writes, so reading it would answer a later run with something nobody decided.
/// </summary>
internal sealed class DecisionFileRefusedException(string message) : Exception(message);

/// <summary>
/// The durable store on disk — plan 00034 step 8: what a save writes beside the baseline, and what a
/// later run begins from.
/// </summary>
/// <remarks>
/// <para>
/// JSON, one object: a <c>version</c>, and the <c>decisions</c> in the order made. Each names its kind
/// and its section by heading path and hash, decision 6's identity. A take and a leave name their source,
/// and a take carries decision 9c's points: the text taken, which its hash is the hash of; whether it was
/// edited; and, when it was, the result.
/// </para>
/// <para>
/// ⛔ <b>Read strictly, as the manifest is.</b> A key the reader does not know, a value of the wrong shape
/// or a version other than its own is refused rather than skipped: a decision dropped in silence is a rule
/// offered again, or one never offered again, with nothing to say why. ⛔ The kinds are closed at
/// decision 10's three, so a deferral cannot come in by hand either; and a take whose hash is not its
/// text's is refused, the two having parted since it was written.
/// </para>
/// <para>
/// Written LF and as UTF-8 without a byte-order mark, as the baseline is — and written whole beside
/// itself, then moved over: ⚠ the file holds every earlier run's decisions as well as this one's, so a
/// write torn half way through would lose them all, where a torn baseline loses one save. ⚠ It holds
/// content too, decision 9c's text and result being the operator's own phrasings (plan 00034 §7): nothing
/// logs it, and it is written only beside the baseline the operator chose.
/// </para>
/// </remarks>
internal static class DecisionFile
{
    /// <summary>The one version this tool writes, and the one it reads.</summary>
    internal const int Version = 1;

    private static readonly HashSet<string> FileKeys = ["version", "decisions"];

    private static readonly HashSet<string> DecisionKeys = ["kind", "headingPath", "hash", "source", "text", "edited", "result"];

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",

        // A rule's own characters as they are — ⛔, an em dash, an ampersand — rather than as escapes, the
        // operator reading this file as much as the tool does. Relaxed escaping is unsafe only inside HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary><paramref name="decisions"/> as a decision file holds them.</summary>
    internal static string Format(IReadOnlyList<SectionDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteStartArray("decisions");
            foreach (SectionDecision decision in decisions)
            {
                WriteDecision(writer, decision);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>The decisions <paramref name="json"/> holds, in the order made.</summary>
    /// <exception cref="DecisionFileRefusedException">It is not a decision file this tool wrote; nothing is read.</exception>
    internal static IReadOnlyList<SectionDecision> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw Refused($"the decision file is not JSON: {ex.Message}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Refused("the decision file is not a JSON object");
            }

            RefuseUnknownKeys(root, FileKeys, "the decision file");
            if (!root.TryGetProperty("version", out JsonElement version))
            {
                throw Refused("the decision file says no \"version\"");
            }

            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != Version)
            {
                throw Refused($"the decision file is version {version.GetRawText()}, and this tool reads version {Version} alone");
            }

            if (!root.TryGetProperty("decisions", out JsonElement list) || list.ValueKind != JsonValueKind.Array)
            {
                throw Refused("the decision file's \"decisions\" is not a list");
            }

            List<SectionDecision> decisions = [];
            foreach (JsonElement item in list.EnumerateArray())
            {
                decisions.Add(ReadDecision(item, $"decision {decisions.Count + 1}"));
            }

            return decisions;
        }
    }

    /// <summary>
    /// Writes <paramref name="decisions"/> to <paramref name="path"/>: whole, to a file beside it, and then
    /// moved over it, so the file it replaces is never part written.
    /// </summary>
    /// <exception cref="IOException">The file cannot be written; one that was there is as it was.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be written; one that was there is as it was.</exception>
    internal static void Write(string path, IReadOnlyList<SectionDecision> decisions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[] bytes = Encoding.UTF8.GetBytes(Format(decisions));
        string partial = path + ".partial";
        try
        {
            File.WriteAllBytes(partial, bytes);
            File.Move(partial, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }
        }
    }

    /// <summary>The decisions the file at <paramref name="path"/> holds, in the order made.</summary>
    /// <exception cref="DecisionFileRefusedException">It is not a decision file this tool wrote.</exception>
    /// <exception cref="IOException">It cannot be read.</exception>
    internal static IReadOnlyList<SectionDecision> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    private static void WriteDecision(Utf8JsonWriter writer, SectionDecision decision)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", decision.Kind switch
        {
            DecisionKind.Taken => "taken",
            DecisionKind.Rejected => "rejected",
            _ => "dropped",
        });
        writer.WriteString("headingPath", decision.Section.HeadingPath);
        writer.WriteString("hash", decision.Section.Hash);
        if (decision.Kind != DecisionKind.Dropped)
        {
            writer.WriteString("source", decision.Source);
        }

        if (decision.Kind == DecisionKind.Taken)
        {
            writer.WriteString("text", decision.Text);
            writer.WriteBoolean("edited", decision.Edited);
            if (decision.Edited)
            {
                // A null result is written as null, not left out: "edited away" is something the file says.
                writer.WriteString("result", decision.Result);
            }
        }

        writer.WriteEndObject();
    }

    private static SectionDecision ReadDecision(JsonElement item, string what)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw Refused($"{what} is not a JSON object");
        }

        RefuseUnknownKeys(item, DecisionKeys, what);

        DecisionKind kind = RequiredString(item, "kind", what) switch
        {
            "taken" => DecisionKind.Taken,
            "rejected" => DecisionKind.Rejected,
            "dropped" => DecisionKind.Dropped,
            string other => throw Refused(
                $"{what}'s \"kind\" is \"{other}\"; it is \"taken\", \"rejected\" or \"dropped\" — what is put off is "
                + "the session's, and never written here"),
        };

        string hash = RequiredString(item, "hash", what);
        if (hash.Length != 64 || !hash.All(char.IsAsciiHexDigitUpper))
        {
            throw Refused($"{what}'s \"hash\" is not a SHA-256 in upper-case hex");
        }

        SectionIdentity section = new(RequiredString(item, "headingPath", what), hash);
        if (kind == DecisionKind.Dropped)
        {
            RefuseKey(item, "source", what, "a drop comes from no candidate");
            RefuseTakesOwn(item, what, "a drop");
            return new SectionDecision(kind, section, null, null);
        }

        string source = RequiredString(item, "source", what);
        if (kind == DecisionKind.Rejected)
        {
            RefuseTakesOwn(item, what, "a leave");
            return new SectionDecision(kind, section, source, null);
        }

        string text = RequiredString(item, "text", what);
        if (SectionIdentity.HashOf(text) != hash)
        {
            throw Refused($"{what}'s \"hash\" is not the hash of its \"text\": the two have parted since it was written");
        }

        if (!RequiredBoolean(item, "edited", what))
        {
            RefuseKey(item, "result", what, "a take that was not edited has none");
            return new SectionDecision(kind, section, source, text);
        }

        if (!item.TryGetProperty("result", out JsonElement result))
        {
            throw Refused($"{what} was edited and says no \"result\"");
        }

        string? edited = result.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => result.GetString(),
            _ => throw Refused($"{what}'s \"result\" is not text or null"),
        };
        if (edited == text)
        {
            throw Refused($"{what} says it was edited, and its \"result\" is the text it took");
        }

        return new SectionDecision(kind, section, source, text) { Edited = true, Result = edited };
    }

    /// <summary>Refuses a take's own keys on a decision of another kind.</summary>
    private static void RefuseTakesOwn(JsonElement item, string what, string which)
    {
        foreach (string key in (string[])["text", "edited", "result"])
        {
            RefuseKey(item, key, what, $"{which} carries no text, and only a take is edited");
        }
    }

    private static void RefuseKey(JsonElement item, string key, string what, string why)
    {
        if (item.TryGetProperty(key, out _))
        {
            throw Refused($"{what} has the key \"{key}\": {why}");
        }
    }

    private static void RefuseUnknownKeys(JsonElement element, HashSet<string> known, string what)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!known.Contains(property.Name))
            {
                throw Refused(
                    $"{what} has a key \"{property.Name}\" it does not know; it knows "
                    + string.Join(", ", known.Order(StringComparer.Ordinal).Select(k => $"\"{k}\"")));
            }
        }
    }

    private static string RequiredString(JsonElement element, string key, string what)
    {
        if (!element.TryGetProperty(key, out JsonElement value))
        {
            throw Refused($"{what} says no \"{key}\"");
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw Refused($"{what}'s \"{key}\" is not a string");
    }

    private static bool RequiredBoolean(JsonElement element, string key, string what)
    {
        if (!element.TryGetProperty(key, out JsonElement value))
        {
            throw Refused($"{what} says no \"{key}\"");
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Refused($"{what}'s \"{key}\" is not true or false"),
        };
    }

    private static DecisionFileRefusedException Refused(string message) => new(message);
}
