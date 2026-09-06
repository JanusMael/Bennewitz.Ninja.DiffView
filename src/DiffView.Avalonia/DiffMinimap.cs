using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>
/// The whole-document overview beside the panes: one pixel row per bucket of rows, each bucket
/// painted in the marker colour of the strongest change it holds (deleted over inserted over
/// modified), the viewport as a translucent rectangle that tracks the scroll, the current
/// change marked at the left edge, a click jumping to the first changed row of its bucket, and
/// a tooltip naming the row under the pointer. The composite feeds it; it renders.
/// </summary>
public class DiffMinimap : Control
{
    /// <summary>Identifies the <see cref="Document"/> property.</summary>
    public static readonly StyledProperty<SideBySideDocument?> DocumentProperty =
        AvaloniaProperty.Register<DiffMinimap, SideBySideDocument?>(nameof(Document));

    /// <summary>Identifies the <see cref="ViewportStartRow"/> property.</summary>
    public static readonly StyledProperty<double> ViewportStartRowProperty =
        AvaloniaProperty.Register<DiffMinimap, double>(nameof(ViewportStartRow));

    /// <summary>Identifies the <see cref="ViewportRowCount"/> property.</summary>
    public static readonly StyledProperty<double> ViewportRowCountProperty =
        AvaloniaProperty.Register<DiffMinimap, double>(nameof(ViewportRowCount));

    /// <summary>Identifies the <see cref="CurrentChangeIndex"/> property.</summary>
    public static readonly StyledProperty<int> CurrentChangeIndexProperty =
        AvaloniaProperty.Register<DiffMinimap, int>(nameof(CurrentChangeIndex), defaultValue: -1);

    /// <summary>Identifies the <see cref="MatchRows"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> MatchRowsProperty =
        AvaloniaProperty.Register<DiffMinimap, IReadOnlyList<int>?>(nameof(MatchRows));

    private const double TickWidth = 4;

    private readonly DiffBrushes _palette = new();
    private DiffLineKind[]? _bucketKinds;
    private SideBySideDocument? _bucketDocument;
    private int _bucketCountCached;
    private bool[]? _matchBuckets;
    private IReadOnlyList<int>? _matchRowsCached;
    private int _matchBucketCountCached;

    static DiffMinimap()
    {
        AffectsRender<DiffMinimap>(DocumentProperty, ViewportStartRowProperty, ViewportRowCountProperty, CurrentChangeIndexProperty, MatchRowsProperty);
    }

    /// <summary>Creates the overview; the composite sets its width and name.</summary>
    public DiffMinimap()
    {
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>A click asked to jump to this row: the first changed row of the clicked bucket, or its first row.</summary>
    public event EventHandler<int>? JumpRequested;

    /// <summary>The model the overview summarises.</summary>
    public SideBySideDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>The first row in the panes' viewport, fractional.</summary>
    public double ViewportStartRow
    {
        get => GetValue(ViewportStartRowProperty);
        set => SetValue(ViewportStartRowProperty, value);
    }

    /// <summary>Rows the panes' viewport shows, fractional.</summary>
    public double ViewportRowCount
    {
        get => GetValue(ViewportRowCountProperty);
        set => SetValue(ViewportRowCountProperty, value);
    }

    /// <summary>The current change block, marked at the left edge; -1 for none.</summary>
    public int CurrentChangeIndex
    {
        get => GetValue(CurrentChangeIndexProperty);
        set => SetValue(CurrentChangeIndexProperty, value);
    }

    /// <summary>The rows holding a find match, marked as ticks down the right edge; <c>null</c> for none.</summary>
    public IReadOnlyList<int>? MatchRows
    {
        get => GetValue(MatchRowsProperty);
        set => SetValue(MatchRowsProperty, value);
    }

    /// <summary>Rows in the model.</summary>
    public int RowCount => Document?.Rows.Count ?? 0;

    /// <summary>Buckets: one per pixel row of the control's height, at least one.</summary>
    public int BucketCount => Math.Max(1, (int)Math.Floor(Bounds.Height));

    /// <summary>The first row of <paramref name="bucket"/>.</summary>
    public int FirstRowOfBucket(int bucket)
    {
        int buckets = BucketCount;
        int clamped = Math.Clamp(bucket, 0, buckets - 1);
        return (int)((long)clamped * RowCount / buckets);
    }

    /// <summary>One past the last row of <paramref name="bucket"/>; equal to its first row when the bucket holds no row.</summary>
    public int EndRowOfBucket(int bucket)
    {
        int buckets = BucketCount;
        int clamped = Math.Clamp(bucket, 0, buckets - 1);
        return (int)((long)(clamped + 1) * RowCount / buckets);
    }

    /// <summary>
    /// The bucket <paramref name="row"/> falls in: the largest bucket whose first row is at or
    /// before it, so the mapping inverts <see cref="FirstRowOfBucket"/> exactly.
    /// </summary>
    public int BucketOfRow(int row)
    {
        int rows = RowCount;
        if (rows == 0)
        {
            return 0;
        }

        int buckets = BucketCount;
        int clamped = Math.Clamp(row, 0, rows - 1);
        return (int)Math.Min(buckets - 1, ((long)clamped * buckets + buckets - 1) / rows);
    }

    /// <summary>The first row of the bucket at pixel row <paramref name="y"/>.</summary>
    public int RowAtPixel(double y)
    {
        return FirstRowOfBucket((int)Math.Floor(y));
    }

    /// <summary>Whether <paramref name="bucket"/> holds a find match.</summary>
    public bool HasMatchInBucket(int bucket)
    {
        bool[] buckets = MatchBuckets();
        return bucket >= 0 && bucket < buckets.Length && buckets[bucket];
    }

    /// <summary>The strongest kind in <paramref name="bucket"/>: deleted over inserted over modified over unchanged.</summary>
    public DiffLineKind KindOfBucket(int bucket)
    {
        DiffLineKind[] kinds = BucketKinds();
        return bucket >= 0 && bucket < kinds.Length ? kinds[bucket] : DiffLineKind.Unchanged;
    }

    /// <summary>The row a click at pixel row <paramref name="y"/> jumps to: the bucket's first changed row, else its first row.</summary>
    public int RowForClick(double y)
    {
        SideBySideDocument? document = Document;
        int bucket = (int)Math.Floor(y);
        int first = FirstRowOfBucket(bucket);
        if (document is null)
        {
            return first;
        }

        int end = Math.Min(EndRowOfBucket(bucket), document.Rows.Count);
        for (int row = first; row < end; row++)
        {
            if (document.Rows[row].Kind != DiffLineKind.Unchanged)
            {
                return row;
            }
        }

        return Math.Min(first, Math.Max(0, document.Rows.Count - 1));
    }

    /// <summary>The tooltip for pixel row <paramref name="y"/>: the row under it and its kind.</summary>
    public string? TooltipFor(double y)
    {
        SideBySideDocument? document = Document;
        if (document is null || document.Rows.Count == 0)
        {
            return null;
        }

        int row = Math.Min(RowAtPixel(y), document.Rows.Count - 1);
        return DiffViewStrings.Format(
            DiffViewStrings.MinimapTooltip,
            (row + 1).ToString("N0", System.Globalization.CultureInfo.CurrentCulture),
            document.Rows.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture),
            DiffViewStrings.KindName(KindOfBucket((int)Math.Floor(y))));
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Rect bounds = new(Bounds.Size);
        context.FillRectangle(_palette[DiffBrush.GutterBackground], bounds);
        SideBySideDocument? document = Document;
        if (document is null || document.Rows.Count == 0 || bounds.Height < 1)
        {
            return;
        }

        DiffLineKind[] kinds = BucketKinds();
        double width = bounds.Width;
        for (int bucket = 0; bucket < kinds.Length; bucket++)
        {
            if (kinds[bucket] != DiffLineKind.Unchanged)
            {
                context.FillRectangle(_palette.MarkerFor(kinds[bucket]), new Rect(0, bucket, width, 1));
            }
        }

        // Find ticks down the right edge, so they read against the kind stripes on the left.
        bool[] matchBuckets = MatchBuckets();
        for (int bucket = 0; bucket < matchBuckets.Length; bucket++)
        {
            if (matchBuckets[bucket])
            {
                context.FillRectangle(_palette[DiffBrush.FindMatch], new Rect(Math.Max(0, width - TickWidth), bucket, Math.Min(TickWidth, width), 1));
            }
        }

        if (CurrentChangeIndex >= 0 && CurrentChangeIndex < document.Blocks.Count)
        {
            ChangeBlock block = document.Blocks[CurrentChangeIndex];
            int top = BucketOfRow(block.FirstRow);
            int bottom = Math.Max(top + 1, BucketOfRow(block.LastRow) + 1);
            context.FillRectangle(_palette[DiffBrush.CurrentBlockBorder], new Rect(0, top, 3, bottom - top));
        }

        if (ViewportRowCount > 0)
        {
            double top = ViewportStartRow * BucketCount / document.Rows.Count;
            double height = Math.Max(2, ViewportRowCount * BucketCount / document.Rows.Count);
            Rect viewport = new(0, Math.Clamp(top, 0, Math.Max(0, bounds.Height - height)), width, Math.Min(height, bounds.Height));
            context.FillRectangle(_palette[DiffBrush.Selection], viewport);
            context.DrawRectangle(new Pen(_palette[DiffBrush.Connector], 1), viewport.Deflate(0.5));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty || change.Property == BoundsProperty)
        {
            _bucketKinds = null;
            _matchBuckets = null;
        }
        else if (change.Property == MatchRowsProperty)
        {
            _matchBuckets = null;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Document is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        JumpRequested?.Invoke(this, RowForClick(e.GetPosition(this).Y));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        ToolTip.SetTip(this, TooltipFor(e.GetPosition(this).Y));
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ToolTip.SetTip(this, null);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        ResourcesChanged += OnPaletteSourceChanged;
        ActualThemeVariantChanged += OnPaletteSourceChanged;
        RefreshPalette();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        ResourcesChanged -= OnPaletteSourceChanged;
        ActualThemeVariantChanged -= OnPaletteSourceChanged;
        base.OnDetachedFromLogicalTree(e);
    }

    private void OnPaletteSourceChanged(object? sender, ResourcesChangedEventArgs e)
    {
        RefreshPalette();
    }

    private void OnPaletteSourceChanged(object? sender, EventArgs e)
    {
        RefreshPalette();
    }

    private void RefreshPalette()
    {
        if (_palette.Resolve(this))
        {
            InvalidateVisual();
        }
    }

    /// <summary>The strongest kind per bucket, computed once per document and height.</summary>
    private DiffLineKind[] BucketKinds()
    {
        SideBySideDocument? document = Document;
        int buckets = BucketCount;
        if (_bucketKinds is not null && ReferenceEquals(_bucketDocument, document) && _bucketCountCached == buckets)
        {
            return _bucketKinds;
        }

        DiffLineKind[] kinds = new DiffLineKind[buckets];
        if (document is not null && document.Rows.Count > 0)
        {
            int rows = document.Rows.Count;
            for (int row = 0; row < rows; row++)
            {
                DiffLineKind kind = document.Rows[row].Kind;
                if (kind == DiffLineKind.Unchanged)
                {
                    continue;
                }

                int bucket = (int)((long)row * buckets / rows);
                if (Strength(kind) > Strength(kinds[bucket]))
                {
                    kinds[bucket] = kind;
                }
            }
        }

        _bucketKinds = kinds;
        _bucketDocument = document;
        _bucketCountCached = buckets;
        return kinds;
    }

    /// <summary>The buckets holding a find match, computed once per match set and height.</summary>
    private bool[] MatchBuckets()
    {
        IReadOnlyList<int>? rows = MatchRows;
        int buckets = BucketCount;
        if (_matchBuckets is not null && ReferenceEquals(_matchRowsCached, rows) && _matchBucketCountCached == buckets)
        {
            return _matchBuckets;
        }

        bool[] marked = new bool[buckets];
        if (rows is not null && RowCount > 0)
        {
            foreach (int row in rows)
            {
                marked[BucketOfRow(row)] = true;
            }
        }

        _matchBuckets = marked;
        _matchRowsCached = rows;
        _matchBucketCountCached = buckets;
        return marked;
    }

    private static int Strength(DiffLineKind kind)
    {
        return kind switch
        {
            DiffLineKind.Deleted => 3,
            DiffLineKind.Inserted => 2,
            DiffLineKind.Modified => 1,
            _ => 0,
        };
    }
}
