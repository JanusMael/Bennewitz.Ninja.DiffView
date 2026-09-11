using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Bennewitz.Ninja.DiffView.Core;

namespace Bennewitz.Ninja.DiffView.Avalonia;

/// <summary>One connector the gutter drew: its block and its four corners in gutter coordinates.</summary>
public sealed record ConnectorPolygon(int BlockIndex, Point LeftTop, Point RightTop, Point RightBottom, Point LeftBottom)
{
    /// <summary>Whether <paramref name="point"/> lies inside the (convex) polygon.</summary>
    public bool Contains(Point point)
    {
        Point[] corners = [LeftTop, RightTop, RightBottom, LeftBottom];
        bool? sign = null;
        for (int i = 0; i < corners.Length; i++)
        {
            Point a = corners[i];
            Point b = corners[(i + 1) % corners.Length];
            double cross = (b.X - a.X) * (point.Y - a.Y) - (b.Y - a.Y) * (point.X - a.X);
            if (Math.Abs(cross) < 1e-9)
            {
                continue;
            }

            bool positive = cross > 0;
            if (sign is null)
            {
                sign = positive;
            }
            else if (sign != positive)
            {
                return false;
            }
        }

        return sign is not null;
    }
}

/// <summary>
/// The column between the panes. For every block in view it draws a polygon joining the rows
/// the block's lines occupy on the left to those on the right — a band where both sides have
/// lines, a wedge where one side has none — filled in the block's tint, the current block
/// outlined. A click on a polygon makes that block the current change; a drag on empty space
/// resizes the panes. This column later hosts the copy-to-side arrows. The composite feeds the
/// row geometry; the gutter renders and hit-tests.
/// </summary>
public class ChangeConnectorGutter : Control
{
    /// <summary>Identifies the <see cref="Document"/> property.</summary>
    public static readonly StyledProperty<SideBySideDocument?> DocumentProperty =
        AvaloniaProperty.Register<ChangeConnectorGutter, SideBySideDocument?>(nameof(Document));

    /// <summary>Identifies the <see cref="RowHeight"/> property.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<ChangeConnectorGutter, double>(nameof(RowHeight));

    /// <summary>Identifies the <see cref="VerticalOffset"/> property.</summary>
    public static readonly StyledProperty<double> VerticalOffsetProperty =
        AvaloniaProperty.Register<ChangeConnectorGutter, double>(nameof(VerticalOffset));

    /// <summary>Identifies the <see cref="ContentOffset"/> property.</summary>
    public static readonly StyledProperty<double> ContentOffsetProperty =
        AvaloniaProperty.Register<ChangeConnectorGutter, double>(nameof(ContentOffset));

    /// <summary>Identifies the <see cref="CurrentChangeIndex"/> property.</summary>
    public static readonly StyledProperty<int> CurrentChangeIndexProperty =
        AvaloniaProperty.Register<ChangeConnectorGutter, int>(nameof(CurrentChangeIndex), defaultValue: -1);

    private const double CurrentOutlineThickness = 2;

    private readonly DiffBrushes _palette = new();
    private readonly List<ConnectorPolygon> _lastPolygons = [];
    private bool _dragging;
    private double _dragLastX;

    static ChangeConnectorGutter()
    {
        AffectsRender<ChangeConnectorGutter>(
            DocumentProperty,
            RowHeightProperty,
            VerticalOffsetProperty,
            ContentOffsetProperty,
            CurrentChangeIndexProperty);
    }

    /// <summary>Creates the gutter; the composite sets its width and name.</summary>
    public ChangeConnectorGutter()
    {
        Focusable = false;
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    /// <summary>A polygon was clicked: its block index.</summary>
    public event EventHandler<int>? BlockClicked;

    /// <summary>The pointer dragged on empty space: the horizontal distance since the last report.</summary>
    public event EventHandler<double>? ResizeDragged;

    /// <summary>The model whose blocks are drawn.</summary>
    public SideBySideDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>The panes' line height: every row is this tall once the padding is primed.</summary>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>The panes' vertical scroll offset.</summary>
    public double VerticalOffset
    {
        get => GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>Where the panes' text starts in the gutter's coordinates.</summary>
    public double ContentOffset
    {
        get => GetValue(ContentOffsetProperty);
        set => SetValue(ContentOffsetProperty, value);
    }

    /// <summary>The current change block, outlined; -1 for none.</summary>
    public int CurrentChangeIndex
    {
        get => GetValue(CurrentChangeIndexProperty);
        set => SetValue(CurrentChangeIndexProperty, value);
    }

    /// <summary>The polygons of the last frame, in block order.</summary>
    public IReadOnlyList<ConnectorPolygon> LastPolygons => _lastPolygons;

    /// <summary>The gutter-relative y of the top of <paramref name="row"/>.</summary>
    public double TopOfRow(int row)
    {
        return row * RowHeight - VerticalOffset + ContentOffset;
    }

    /// <summary>
    /// The row under <paramref name="y"/> — <see cref="TopOfRow"/> read backwards — or
    /// <c>null</c> before the first row and before the rows have a height.
    /// </summary>
    public int? RowAt(double y)
    {
        double rowHeight = RowHeight;
        if (rowHeight <= 0)
        {
            return null;
        }

        int row = (int)Math.Floor((y + VerticalOffset - ContentOffset) / rowHeight);
        return row < 0 ? null : row;
    }

    /// <summary>The polygon under <paramref name="point"/> in the last frame, or <c>null</c>.</summary>
    public ConnectorPolygon? PolygonAt(Point point)
    {
        return _lastPolygons.FirstOrDefault(p => p.Contains(point));
    }

    /// <summary>The tooltip for <paramref name="point"/>: the block under it, or none.</summary>
    public string? TooltipFor(Point point)
    {
        SideBySideDocument? document = Document;
        if (document is null || PolygonAt(point) is not { } polygon)
        {
            return null;
        }

        ChangeBlock block = document.Blocks[polygon.BlockIndex];
        return DiffViewStrings.Format(
            DiffViewStrings.MarkerTooltip,
            (block.Index + 1).ToString("N0", CultureInfo.CurrentCulture),
            document.Blocks.Count.ToString("N0", CultureInfo.CurrentCulture),
            DiffViewStrings.Format(DiffViewStrings.StatusCounts, block.InsertedCount, block.DeletedCount, block.ModifiedCount));
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _lastPolygons.Clear();
        Rect bounds = new(Bounds.Size);
        context.FillRectangle(_palette[DiffBrush.GutterBackground], bounds);
        SideBySideDocument? document = Document;
        double rowHeight = RowHeight;
        if (document is null || rowHeight <= 0 || bounds.Height <= 0)
        {
            return;
        }

        int firstVisible = Math.Max(0, (int)Math.Floor((VerticalOffset - ContentOffset) / rowHeight) - 1);
        int lastVisible = (int)Math.Ceiling((VerticalOffset - ContentOffset + bounds.Height) / rowHeight) + 1;
        double width = bounds.Width;
        Pen outline = new(_palette[DiffBrush.CurrentBlockBorder], CurrentOutlineThickness);

        foreach (ChangeBlock block in document.Blocks)
        {
            if (block.LastRow < firstVisible)
            {
                continue;
            }

            if (block.FirstRow > lastVisible)
            {
                break;
            }

            // The pairing rule puts modified rows first, then the side's own rows: the left's
            // lines occupy the first modified + deleted rows, the right's the first modified + inserted.
            double top = TopOfRow(block.FirstRow);
            double leftBottom = TopOfRow(block.FirstRow + block.ModifiedCount + block.DeletedCount);
            double rightBottom = TopOfRow(block.FirstRow + block.ModifiedCount + block.InsertedCount);
            ConnectorPolygon polygon = new(block.Index, new Point(0, top), new Point(width, top), new Point(width, rightBottom), new Point(0, leftBottom));
            _lastPolygons.Add(polygon);

            StreamGeometry geometry = new();
            using (StreamGeometryContext path = geometry.Open())
            {
                path.BeginFigure(polygon.LeftTop, isFilled: true);
                path.LineTo(polygon.RightTop);
                path.LineTo(polygon.RightBottom);
                path.LineTo(polygon.LeftBottom);
                path.EndFigure(isClosed: true);
            }

            bool current = block.Index == CurrentChangeIndex;
            context.DrawGeometry(_palette.ForKind(block.Kind), current ? outline : null, geometry);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point point = e.GetPosition(this);
        if (PolygonAt(point) is { } polygon)
        {
            BlockClicked?.Invoke(this, polygon.BlockIndex);
            e.Handled = true;
            return;
        }

        _dragging = true;
        _dragLastX = e.GetPosition(Parent as Visual ?? this).X;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            double x = e.GetPosition(Parent as Visual ?? this).X;
            double delta = x - _dragLastX;
            _dragLastX = x;
            if (Math.Abs(delta) > 0)
            {
                ResizeDragged?.Invoke(this, delta);
            }

            e.Handled = true;
            return;
        }

        Point point = e.GetPosition(this);
        bool overBlock = PolygonAt(point) is not null;
        Cursor = new Cursor(overBlock ? StandardCursorType.Hand : StandardCursorType.SizeWestEast);
        ToolTip.SetTip(this, TooltipFor(point));
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
}
