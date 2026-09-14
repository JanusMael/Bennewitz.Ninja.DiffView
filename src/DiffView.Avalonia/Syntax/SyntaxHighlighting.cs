using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The grammar chosen for a file: the language it belongs to and the TextMate scope the registry
/// loads it by.
/// </summary>
/// <param name="LanguageId">The language as TextMateSharp names it — <c>csharp</c>, <c>json</c>; what a message names.</param>
/// <param name="ScopeName">The scope the grammar is registered under — <c>source.cs</c>.</param>
internal readonly record struct SyntaxGrammar(string LanguageId, string ScopeName);

/// <summary>
/// The TextMate side of one pane: the grammar registry, the installation over the editor, and the
/// theme that follows the variant. It colours text only — the foreground of a token — so the diff
/// backgrounds, the word-level pieces, the match highlights and the selection all compose over it
/// unchanged.
/// </summary>
/// <remarks>
/// <para>
/// The registry is built on first use and kept: constructing it reads the grammar index out of
/// TextMateSharp's resources, so a pane whose file no grammar claims never pays for it, and one
/// that switches between files pays once. It is per pane rather than shared because TextMateSharp
/// tokenizes on its own thread and may reach back into the registry for an embedded grammar.
/// </para>
/// <para>
/// Nothing here throws on the caller's behalf: <see cref="GrammarFor"/> and <see cref="Apply"/>
/// let a registry or grammar failure out, and the exception handler passed to the installation
/// catches what the tokenizer thread raises later. <see cref="DiffPanePresenter"/> turns either
/// into a fault, which is what puts the control in <see cref="DiffViewState.Degraded"/>.
/// </para>
/// </remarks>
internal sealed class SyntaxHighlighting
{
    private readonly TextEditor _editor;
    private readonly Action<Exception> _onException;
    private RegistryOptions? _registry;
    private TextMateInstallation? _installation;

    /// <param name="editor">The editor to colour.</param>
    /// <param name="onException">
    /// Receives what the installation throws after it is installed — tokenization and document
    /// changes run on TextMateSharp's own thread, so this is called off the UI thread.
    /// </param>
    public SyntaxHighlighting(TextEditor editor, Action<Exception> onException)
    {
        _editor = editor;
        _onException = onException;
    }

    /// <summary>The grammar currently installed, or <c>null</c> while the pane is plain text.</summary>
    public SyntaxGrammar? Installed { get; private set; }

    /// <summary>
    /// The grammar for <paramref name="fileName"/> — a name or a path — or <c>null</c> when it has
    /// no extension or no grammar claims it, which is plain text and not a failure.
    /// </summary>
    public SyntaxGrammar? GrammarFor(string? fileName, ThemeVariant variant)
    {
        string extension = System.IO.Path.GetExtension(fileName ?? string.Empty);
        if (extension.Length <= 1)
        {
            return null;
        }

        RegistryOptions registry = Registry(variant);
        Language? language = registry.GetLanguageByExtension(extension);
        if (language?.Id is not { Length: > 0 } languageId)
        {
            return null;
        }

        string? scopeName = registry.GetScopeByLanguageId(languageId);
        return scopeName is { Length: > 0 } ? new SyntaxGrammar(languageId, scopeName) : null;
    }

    /// <summary>
    /// Installs <paramref name="grammar"/>, with the theme <paramref name="variant"/> asks for,
    /// creating the installation on first use.
    /// </summary>
    public void Apply(SyntaxGrammar grammar, ThemeVariant variant)
    {
        RegistryOptions registry = Registry(variant);
        _installation ??= _editor.InstallTextMate(registry, initCurrentDocument: true, exceptionHandler: _onException);
        _installation.SetTheme(registry.LoadTheme(ThemeNameFor(variant)));
        _installation.SetGrammar(grammar.ScopeName);
        Installed = grammar;
    }

    /// <summary>Re-applies the theme for <paramref name="variant"/>; a no-op while nothing is installed.</summary>
    public void SetTheme(ThemeVariant variant)
    {
        _installation?.SetTheme(Registry(variant).LoadTheme(ThemeNameFor(variant)));
    }

    /// <summary>
    /// Removes the installation, leaving the text plain and the registry in hand for the next
    /// file. Returns whether there was one to remove, so the caller only redraws when the pane
    /// actually lost its colours.
    /// </summary>
    public bool Remove()
    {
        if (_installation is null)
        {
            return false;
        }

        _installation.Dispose();
        _installation = null;
        Installed = null;
        return true;
    }

    /// <summary>Dark themes get Dark+, everything else Light+: the variant decides, never the diff palette.</summary>
    private static ThemeName ThemeNameFor(ThemeVariant variant)
    {
        return variant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
    }

    private RegistryOptions Registry(ThemeVariant variant)
    {
        // The theme named here is only the registry's default; every path sets the theme it wants.
        return _registry ??= new RegistryOptions(ThemeNameFor(variant));
    }
}
