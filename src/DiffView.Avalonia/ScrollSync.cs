using Avalonia;
using Avalonia.Controls;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// Two-way coupling of two scroll viewers' vertical offsets — and their horizontal offsets when
/// <see cref="SyncHorizontal"/> is on — with a re-entrancy guard and a compare-before-set, so a
/// change on either side lands once on the other and never feeds back. After priming both panes
/// have the same number of rows at the same line height with no wrapping, so their extents are
/// equal and the coupling is a 1:1 offset copy.
/// </summary>
internal sealed class ScrollSync : IDisposable
{
    private const double Tolerance = 1e-6;

    private readonly ScrollViewer _left;
    private readonly ScrollViewer _right;
    private bool _syncing;

    public ScrollSync(ScrollViewer left, ScrollViewer right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        _left = left;
        _right = right;
        _left.ScrollChanged += OnLeftScrolled;
        _right.ScrollChanged += OnRightScrolled;
    }

    /// <summary>Whether the horizontal offsets are coupled too.</summary>
    public bool SyncHorizontal { get; set; }

    /// <summary>Copies the left offset to the right now, for a freshly coupled pair.</summary>
    public void Align()
    {
        Follow(_left, _right);
    }

    public void Dispose()
    {
        _left.ScrollChanged -= OnLeftScrolled;
        _right.ScrollChanged -= OnRightScrolled;
    }

    private void OnLeftScrolled(object? sender, ScrollChangedEventArgs e)
    {
        Follow(_left, _right);
    }

    private void OnRightScrolled(object? sender, ScrollChangedEventArgs e)
    {
        Follow(_right, _left);
    }

    private void Follow(ScrollViewer source, ScrollViewer target)
    {
        if (_syncing)
        {
            return;
        }

        Vector wanted = new(SyncHorizontal ? source.Offset.X : target.Offset.X, source.Offset.Y);
        if (Math.Abs(target.Offset.Y - wanted.Y) < Tolerance && Math.Abs(target.Offset.X - wanted.X) < Tolerance)
        {
            return;
        }

        _syncing = true;
        try
        {
            target.Offset = wanted;
        }
        finally
        {
            _syncing = false;
        }
    }
}
