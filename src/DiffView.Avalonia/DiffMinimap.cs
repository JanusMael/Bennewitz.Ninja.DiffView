using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView;

/// <summary>
/// The whole-document overview beside the panes: one pixel row per bucket of rows, in **two
/// lanes**, one per side. A bucket inks a lane only where that side has a changed line in the
/// rows it covers, so a deletion inks the left lane and notches the right, an insertion does the
/// reverse, and a modification inks both — which is what one lane cannot say. Over them: the
/// viewport as a translucent rectangle tracking the scroll, the current change marked in its own
/// column at the left edge, find ticks on the outer edge, a click jumping to the first changed
/// row of its bucket, a drag of the viewport scrolling continuously, and a tooltip naming the row
/// and the lane under the pointer. The composite feeds it; it renders.
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

    /// <summary>Identifies the <see cref="MirrorEdges"/> property.</summary>
    public static readonly StyledProperty<bool> MirrorEdgesProperty =
        AvaloniaProperty.Register<DiffMinimap, bool>(nameof(MirrorEdges));

    /// <summary>Identifies the <see cref="MatchRows"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> MatchRowsProperty =
        AvaloniaProperty.Register<DiffMinimap, IReadOnlyList<int>?>(nameof(MatchRows));

    private const double TickWidth = 4;

    /// <summary>The column the map asks for: the marker column, two lanes, the gap between them and a margin.</summary>
    public const double MapWidth = MarkerColumnWidth + (2 * LaneWidth) + LaneGap + LaneMargin;

    /// <summary>The current change's own column, at the left edge; it belongs to neither side.</summary>
    private const double MarkerColumnWidth = 2;

    /// <summary>One side's lane, and the unpainted gap that keeps the two readable as two.</summary>
    private const double LaneWidth = 8;
    private const double LaneGap = 2;

    /// <summary>What is left at the outer edge, under the find ticks.</summary>
    private const double LaneMargin = 2;

    /// <summary>Rows a wheel notch moves the viewport.</summary>
    private const int WheelRows = 3;

    private readonly DiffBrushes _palette = new();
    private RowProjection? _projection;
    private DiffLineKind[]? _leftKinds;
    private DiffLineKind[]? _rightKinds;
    private SideBySideDocument? _bucketDocument;
    private int _bucketCountCached;
    private bool[]? _matchBuckets;
    private bool _dragging;
    private IReadOnlyList<int>? _matchRowsCached;
    private int _matchBucketCountCached;

    static DiffMinimap()
    {
        AffectsRender<DiffMinimap>(DocumentProperty, ViewportStartRowProperty, ViewportRowCountProperty, CurrentChangeIndexProperty, MatchRowsProperty, MirrorEdgesProperty);
    }

    /// <summary>Creates the overview; the composite sets its width and name.</summary>
    public DiffMinimap()
    {
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.Hand);
        // The template's column is Auto so that hiding the control gives the width back to the
        // panes rather than leaving a gap; the control is what names the width.
        Width = MapWidth;
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

    /// <summary>
    /// Whether the edges are mirrored: set when the panes are to the map's <em>right</em>, which
    /// is what docking on the left means. The marker column hugs the panes and the find ticks hug
    /// the outside, so both swap ends with this — and the two lanes do not, because a lane names a
    /// file and not an edge. The map never learns which side of the window it is on, only which of
    /// its own edges faces the panes.
    /// </summary>
    public bool MirrorEdges
    {
        get => GetValue(MirrorEdgesProperty);
        set => SetValue(MirrorEdgesProperty, value);
    }

    /// <summary>The rows holding a find match, marked as ticks down the right edge; <c>null</c> for none.</summary>
    public IReadOnlyList<int>? MatchRows
    {
        get => GetValue(MatchRowsProperty);
        set => SetValue(MatchRowsProperty, value);
    }

    /// <summary>Rows in the model, folded or not.</summary>
    public int RowCount => Document?.Rows.Count ?? 0;

    /// <summary>
    /// How the model's rows sit on screen. Null is the identity, which is what a map with nothing
    /// folded has always assumed. The map draws the <i>visible</i> document: a bucket over rows
    /// the panes are not showing would put the viewport box where the viewport is not.
    /// </summary>
    internal RowProjection? Projection
    {
        get => _projection;
        set
        {
            if (!ReferenceEquals(_projection, value))
            {
                _projection = value;
                _leftKinds = null;
                _rightKinds = null;
                _matchBuckets = null;
                InvalidateVisual();
            }
        }
    }

    /// <summary>Rows on screen: <see cref="RowCount"/> less what the folds hide.</summary>
    internal int VisibleRowCount => _projection?.VisibleRowCount ?? RowCount;

    /// <summary>Buckets: one per pixel row of the control's height, at least one.</summary>
    public int BucketCount => Math.Max(1, (int)Math.Floor(Bounds.Height));

    /// <summary>The first row of <paramref name="bucket"/>, as a model row.</summary>
    public int FirstRowOfBucket(int bucket)
    {
        int buckets = BucketCount;
        int clamped = Math.Clamp(bucket, 0, buckets - 1);
        return ModelRowOf((int)((long)clamped * VisibleRowCount / buckets));
    }

    private int VisibleRowOf(int modelRow)
    {
        return _projection?.VisibleRowOf(modelRow) ?? modelRow;
    }

    private int ModelRowOf(int visibleRow)
    {
        return _projection?.ModelRowOf(visibleRow) ?? visibleRow;
    }

    /// <summary>
    /// One past the last model row of <paramref name="bucket"/>; equal to its first row when the
    /// bucket holds no row. A bucket ending past the last visible row ends at the model's end.
    /// </summary>
    public int EndRowOfBucket(int bucket)
    {
        int buckets = BucketCount;
        int clamped = Math.Clamp(bucket, 0, buckets - 1);
        int visible = (int)((long)(clamped + 1) * VisibleRowCount / buckets);
        return visible >= VisibleRowCount ? RowCount : ModelRowOf(visible);
    }

    /// <summary>
    /// The bucket model row <paramref name="row"/> falls in: the largest bucket whose first row is
    /// at or before it, so the mapping inverts <see cref="FirstRowOfBucket"/> exactly.
    /// </summary>
    public int BucketOfRow(int row)
    {
        int rows = VisibleRowCount;
        if (rows == 0)
        {
            return 0;
        }

        int buckets = BucketCount;
        int clamped = Math.Clamp(VisibleRowOf(row), 0, rows - 1);
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

    /// <summary>
    /// The strongest kind in <paramref name="bucket"/> on either side: deleted over inserted over
    /// modified over unchanged. Every changed row belongs to at least one side's lane, so this is
    /// the same answer the single-lane map gave.
    /// </summary>
    public DiffLineKind KindOfBucket(int bucket)
    {
        DiffLineKind left = KindOfBucket(bucket, DiffSide.Left);
        DiffLineKind right = KindOfBucket(bucket, DiffSide.Right);
        return Strength(left) >= Strength(right) ? left : right;
    }

    /// <summary>
    /// The strongest kind in <paramref name="bucket"/> that <paramref name="side"/> has a line
    /// for. Rows the side pads are absence, and absence is what a notch in one lane means.
    /// </summary>
    public DiffLineKind KindOfBucket(int bucket, DiffSide side)
    {
        DiffLineKind[] kinds = BucketKinds(side);
        return bucket >= 0 && bucket < kinds.Length ? kinds[bucket] : DiffLineKind.Unchanged;
    }

    /// <summary>The lane <paramref name="x"/> falls in, or <c>null</c> for the marker column, the gap or the margin.</summary>
    public DiffSide? LaneAt(double x)
    {
        foreach (DiffSide side in (DiffSide[])[DiffSide.Left, DiffSide.Right])
        {
            double left = LaneLeft(side);
            if (x >= left && x < left + LaneWidth)
            {
                return side;
            }
        }

        return null;
    }

    /// <summary>Where <paramref name="side"/>'s lane starts, in control coordinates.</summary>
    /// <remarks>
    /// The lanes sit between the marker column and the margin whichever way round those two are,
    /// and in the same order either way: the left file's lane is the left one, always.
    /// </remarks>
    private double LaneLeft(DiffSide side)
    {
        double lanes = MirrorEdges ? LaneMargin : MarkerColumnWidth;
        return side == DiffSide.Left ? lanes : lanes + LaneWidth + LaneGap;
    }

    /// <summary>Where the current-block marker's column starts: the edge nearest the panes.</summary>
    private double MarkerLeft => MirrorEdges ? Bounds.Width - MarkerColumnWidth : 0;

    /// <summary>Where the find ticks start: the edge away from the panes, out of the lanes' way.</summary>
    private double TicksLeft => MirrorEdges ? 0 : Math.Max(0, Bounds.Width - TickWidth);

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

        return TooltipFor(0, y);
    }

    /// <summary>
    /// The tooltip at a point: the row under it, and — when the point is inside a lane — that
    /// side's own kind there, which is the question the two lanes exist to answer.
    /// </summary>
    public string? TooltipFor(double x, double y)
    {
        SideBySideDocument? document = Document;
        if (document is null || document.Rows.Count == 0)
        {
            return null;
        }

        int row = Math.Min(RowAtPixel(y), document.Rows.Count - 1);
        int bucket = (int)Math.Floor(y);
        string number = (row + 1).ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
        string total = document.Rows.Count.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);

        if (LaneAt(x) is not { } side)
        {
            return DiffViewStrings.Format(
                DiffViewStrings.MinimapTooltip, number, total, DiffViewStrings.KindName(KindOfBucket(bucket)));
        }

        // The lane's side picks between two whole sentences; the kind stays a placeholder, because
        // it stands on its own between separators rather than inside a phrase.
        return DiffViewStrings.MinimapLaneTooltip(
            side,
            number,
            total,
            DiffViewStrings.KindName(KindOfBucket(bucket, side)));
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

        double width = bounds.Width;
        foreach (DiffSide side in (DiffSide[])[DiffSide.Left, DiffSide.Right])
        {
            DiffLineKind[] kinds = BucketKinds(side);
            double left = LaneLeft(side);
            for (int bucket = 0; bucket < kinds.Length; bucket++)
            {
                if (kinds[bucket] != DiffLineKind.Unchanged)
                {
                    context.FillRectangle(_palette.MarkerFor(kinds[bucket]), new Rect(left, bucket, LaneWidth, 1));
                }
            }
        }

        // Find ticks down the right edge, so they read against the kind stripes on the left.
        bool[] matchBuckets = MatchBuckets();
        for (int bucket = 0; bucket < matchBuckets.Length; bucket++)
        {
            if (matchBuckets[bucket])
            {
                context.FillRectangle(_palette[DiffBrush.FindMatch], new Rect(TicksLeft, bucket, Math.Min(TickWidth, width), 1));
            }
        }

        if (CurrentChangeIndex >= 0 && CurrentChangeIndex < document.Blocks.Count)
        {
            ChangeBlock block = document.Blocks[CurrentChangeIndex];
            int top = BucketOfRow(block.FirstRow);
            int bottom = Math.Max(top + 1, BucketOfRow(block.LastRow) + 1);
            // Its own column against the panes: the current block belongs to the pair, not to a
            // side, and it points into the text rather than away from it.
            context.FillRectangle(_palette[DiffBrush.CurrentBlockBorder], new Rect(MarkerLeft, top, MarkerColumnWidth, bottom - top));
        }

        if (ViewportBounds is { } viewport)
        {
            context.FillRectangle(_palette[DiffBrush.Selection], viewport);
            context.DrawRectangle(new Pen(_palette[DiffBrush.Connector], 1), viewport.Deflate(0.5));
        }
    }

    /// <summary>
    /// The viewport rectangle, or <c>null</c> when there is nothing to show one over. Exposed
    /// because it is the boundary between the two pointer gestures: a press inside it drags, a
    /// press outside it jumps, and the two must not be left to reading order.
    /// </summary>
    public Rect? ViewportBounds
    {
        get
        {
            SideBySideDocument? document = Document;
            if (document is null || document.Rows.Count == 0 || ViewportRowCount <= 0 || Bounds.Height < 1)
            {
                return null;
            }

            // The viewport's start and count are pixels over the line height, so they are visible
            // rows already, and the buckets they scale into are the visible document's.
            int rows = Math.Max(1, VisibleRowCount);
            double top = ViewportStartRow * BucketCount / rows;
            double height = Math.Max(2, ViewportRowCount * BucketCount / rows);
            return new Rect(
                0,
                Math.Clamp(top, 0, Math.Max(0, Bounds.Height - height)),
                Bounds.Width,
                Math.Min(height, Bounds.Height));
        }
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty || change.Property == BoundsProperty)
        {
            _leftKinds = null;
            _rightKinds = null;
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

        Point point = e.GetPosition(this);

        // Inside the viewport box the gesture is a drag, outside it a jump. The two never contest
        // a pixel, because the box is exactly what separates them.
        if (ViewportBounds is { } viewport && viewport.Contains(point))
        {
            _dragging = true;
            e.Pointer.Capture(this);
            ScrollTo(point.Y);
            e.Handled = true;
            return;
        }

        JumpRequested?.Invoke(this, RowForClick(point.Y));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
        {
            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// A notch over the map scrolls the panes rather than the page, by
    /// <see cref="WheelRows"/> rows — the same event a click raises, so nothing new crosses the
    /// boundary between the map and the panes.
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Document is not { } document || document.Rows.Count == 0)
        {
            return;
        }

        // The viewport is in visible rows; the jump is asked for in model rows.
        int centre = (int)Math.Round(ViewportStartRow + (ViewportRowCount / 2));
        int target = centre - ((int)Math.Round(e.Delta.Y) * WheelRows);
        JumpRequested?.Invoke(this, ModelRowOf(Math.Clamp(target, 0, Math.Max(0, VisibleRowCount - 1))));
        e.Handled = true;
    }

    /// <summary>Scrolls so the row under <paramref name="y"/> is where the drag put it.</summary>
    private void ScrollTo(double y)
    {
        if (Document is not { } document || document.Rows.Count == 0)
        {
            return;
        }

        int visible = VisibleRowOf(RowAtPixel(y)) - (int)Math.Round(ViewportRowCount / 2);
        JumpRequested?.Invoke(this, ModelRowOf(Math.Clamp(visible, 0, Math.Max(0, VisibleRowCount - 1))));
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Point point = e.GetPosition(this);
        if (_dragging)
        {
            ScrollTo(point.Y);
            e.Handled = true;
            return;
        }

        ToolTip.SetTip(this, TooltipFor(point.X, point.Y));
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

    /// <summary>
    /// The strongest kind per bucket that <paramref name="side"/> has a line for, computed once
    /// per document and height. A row the side pads contributes nothing, which is what leaves the
    /// other lane's block a notch rather than a band.
    /// </summary>
    private DiffLineKind[] BucketKinds(DiffSide side)
    {
        SideBySideDocument? document = Document;
        int buckets = BucketCount;
        bool cached = ReferenceEquals(_bucketDocument, document) && _bucketCountCached == buckets;
        if (cached && (side == DiffSide.Left ? _leftKinds : _rightKinds) is { } hit)
        {
            return hit;
        }

        if (!cached)
        {
            _leftKinds = null;
            _rightKinds = null;
        }

        DiffLineKind[] kinds = new DiffLineKind[buckets];
        int visibleRows = VisibleRowCount;
        if (document is not null && document.Rows.Count > 0 && visibleRows > 0)
        {
            int rows = document.Rows.Count;
            for (int row = 0; row < rows; row++)
            {
                AlignedRow aligned = document.Rows[row];
                if (aligned.Kind == DiffLineKind.Unchanged || SideBySideDocument.LineOf(aligned, side) is null)
                {
                    continue;
                }

                // A row behind a placeholder is not on screen, so it colours no bucket. A folded
                // run is unchanged by construction, so this only ever skips rows already skipped.
                if (_projection?.IsHidden(row) == true)
                {
                    continue;
                }

                int bucket = (int)((long)VisibleRowOf(row) * buckets / visibleRows);
                if (Strength(aligned.Kind) > Strength(kinds[bucket]))
                {
                    kinds[bucket] = aligned.Kind;
                }
            }
        }

        if (side == DiffSide.Left)
        {
            _leftKinds = kinds;
        }
        else
        {
            _rightKinds = kinds;
        }

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
        if (rows is not null && VisibleRowCount > 0)
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
