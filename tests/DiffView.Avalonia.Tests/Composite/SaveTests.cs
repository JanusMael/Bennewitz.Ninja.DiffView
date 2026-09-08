using System.Text;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia.Tests.Composite;

/// <summary>
/// Plan 00003 §Phase 3, dirty state and save. A pane writes back to the file it was read from,
/// with that file's encoding, byte-order mark and line terminators — and never over someone
/// else's write.
/// </summary>
public sealed class SaveTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"diffview-save-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leaked temp directory is not worth failing a test over.
        }
    }

    [AvaloniaFact]
    public async Task An_edited_pane_is_dirty_saves_and_comes_back_clean()
    {
        string path = WriteFile("left.txt", Encoding.UTF8, "alpha\r\nbeta\r\ngamma\r\n");
        byte[] before = File.ReadAllBytes(path);

        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(PaneSource.FromFile(path), "alpha\r\nBETA\r\ngamma\r\n");

        Assert.False(host.View.IsDirty(DiffSide.Left));
        Assert.False(host.View.CanSave(DiffSide.Left));

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        Edit(host, "// added\n");

        Assert.True(host.View.IsDirty(DiffSide.Left));
        Assert.True(host.View.CanSave(DiffSide.Left));
        Assert.True(host.View.LeftHeader!.IsDirty);

        Assert.Equal(SaveOutcome.Saved, host.View.Save(DiffSide.Left));
        CompositeHost.Layout();

        Assert.False(host.View.IsDirty(DiffSide.Left));
        Assert.False(host.View.LeftHeader!.IsDirty);

        // The file moved, and the CRLF convention it arrived with survived the trip.
        byte[] after = File.ReadAllBytes(path);
        Assert.NotEqual(before, after);
        string written = Encoding.UTF8.GetString(after, 3, after.Length - 3);
        Assert.StartsWith("// added\r\n", written, StringComparison.Ordinal);
        Assert.DoesNotContain("added\n\r", written, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, after.AsSpan(0, 3).ToArray());

        // Still edited relative to the source it was assigned, so the next build reads the document.
        Assert.True(host.View.IsEdited(DiffSide.Left));
    }

    [AvaloniaFact]
    public async Task A_file_changed_on_disk_is_reported_and_nothing_is_written()
    {
        string path = WriteFile("left.txt", Encoding.UTF8, "one\ntwo\n");

        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(PaneSource.FromFile(path), "one\nTWO\n");
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        Edit(host, "mine\n");

        // Someone else writes while the pane holds edits.
        File.WriteAllText(path, "theirs\nentirely\n");
        byte[] theirs = File.ReadAllBytes(path);

        Assert.Equal(SaveOutcome.ChangedOnDisk, host.View.Save(DiffSide.Left));
        CompositeHost.Layout();

        Assert.Equal(theirs, File.ReadAllBytes(path));
        Assert.True(host.View.IsDirty(DiffSide.Left));
        Assert.StartsWith("mine", host.Left.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_side_with_no_file_reports_rather_than_throwing()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        Edit(host, "x");

        Assert.True(host.View.IsDirty(DiffSide.Left));
        Assert.False(host.View.CanSave(DiffSide.Left));
        Assert.Equal(SaveOutcome.NoPath, host.View.Save(DiffSide.Left));
    }

    [AvaloniaFact]
    public async Task Saving_a_clean_side_writes_nothing()
    {
        string path = WriteFile("left.txt", Encoding.UTF8, "unchanged\n");
        DateTime before = File.GetLastWriteTimeUtc(path);

        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(PaneSource.FromFile(path), "different\n");

        Assert.Equal(SaveOutcome.NotDirty, host.View.Save(DiffSide.Left));
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [AvaloniaFact]
    public async Task Revert_puts_the_source_text_back_and_rebuilds()
    {
        (string left, string right) = CompositeHost.SmallFixture();
        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(left, right);
        int blocksBefore = host.View.Document!.Blocks.Count;

        host.View.LeftReadOnly = false;
        CompositeHost.Layout();
        Edit(host, "// a line only the left side has\n");
        await host.WaitForReDiffAsync();
        Assert.NotEqual(blocksBefore, host.View.Document!.Blocks.Count);

        host.View.Revert(DiffSide.Left);
        await host.WaitForBuildAsync();

        // Back to the assigned text, clean, unedited, and the model agrees.
        Assert.Equal(left, host.Left.Document.Text);
        Assert.False(host.View.IsDirty(DiffSide.Left));
        Assert.False(host.View.IsEdited(DiffSide.Left));
        Assert.False(host.View.LeftHeader!.IsDirty);
        Assert.Equal(blocksBefore, host.View.Document!.Blocks.Count);
    }

    [AvaloniaFact]
    public async Task A_save_lets_the_next_one_through_rather_than_seeing_its_own_write_as_a_conflict()
    {
        string path = WriteFile("left.txt", Encoding.UTF8, "one\n");

        using CompositeHost host = new();
        host.Show();
        await host.LoadAsync(PaneSource.FromFile(path), "two\n");
        host.View.LeftReadOnly = false;
        CompositeHost.Layout();

        Edit(host, "first\n");
        Assert.Equal(SaveOutcome.Saved, host.View.Save(DiffSide.Left));

        // The stamp has to follow our own write, or every save after the first would conflict.
        Edit(host, "second\n");
        Assert.Equal(SaveOutcome.Saved, host.View.Save(DiffSide.Left));
        Assert.StartsWith("second", File.ReadAllText(path).TrimStart('﻿'), StringComparison.Ordinal);
    }

    private static void Edit(CompositeHost host, string text)
    {
        host.Left.TextArea.Focus();
        host.Left.TextArea.Caret.Offset = 0;
        CompositeHost.Layout();
        host.Window.KeyTextInput(text);
        CompositeHost.Layout();
    }

    private string WriteFile(string name, Encoding encoding, string text)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [.. encoding.GetPreamble(), .. encoding.GetBytes(text)]);
        return path;
    }
}
