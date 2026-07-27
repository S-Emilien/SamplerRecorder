using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SamplerRecorder.Models;
using SamplerRecorder.Services;

namespace SamplerRecorder.Controls;

/// <summary>
/// Custom WPF control that renders a waveform with markers, selection, and playback cursor.
/// Uses DrawingVisual for high-performance rendering.
/// </summary>
public class WaveformControl : FrameworkElement
{
    private readonly DrawingVisual _visual = new();
    private readonly List<Visual> _visuals = new();

    // Interaction state
    private bool _isPanning;           // left-drag pans the view
    private bool _isSelecting;         // right-drag creates selection
    private bool _isResizingLeft;
    private bool _isResizingRight;
    private bool _leftDown;
    private bool _leftDragConfirmed;   // true once movement exceeds threshold
    private Point _dragStart;
    private double _dragStartViewMs;
    private const double HandleWidth = 8;    // pixels for resize handle hit zone
    private const double DragThreshold = 5;  // pixels before a left-click becomes a pan

    // Dependencies
    public WaveformDataService? WaveformService { get; set; }
    public List<Marker>? Markers { get; set; }

    // View properties
    public double ViewStartMs { get; set; }
    public double ViewEndMs { get; set; } = 60000;
    public double PlaybackPositionMs { get; set; } = -1;
    public long SelectionStartMs { get; set; } = -1;
    public long SelectionEndMs { get; set; } = -1;

    /// <summary>True while a right-button selection/resize drag is in progress.</summary>
    public bool IsSelectingInteraction => _isSelecting || _isResizingLeft || _isResizingRight;

    // Events
    public event Action<double, double>? ViewChanged;
    public event Action<long, long>? SelectionChanged;
    public event Action<double>? SeekRequested;

    // Colors
    private static readonly Color BgColor = Color.FromRgb(24, 24, 32);
    private static readonly Color WaveColor = Color.FromRgb(80, 180, 255);
    private static readonly Color WaveFillColor = Color.FromArgb(120, 80, 180, 255);
    private static readonly Color MarkerColor = Color.FromRgb(255, 200, 50);
    private static readonly Color SelectionColor = Color.FromArgb(60, 255, 255, 255);
    private static readonly Color CursorColor = Color.FromRgb(255, 80, 80);
    private static readonly Color GridColor = Color.FromArgb(40, 255, 255, 255);
    private static readonly Color CenterLineColor = Color.FromArgb(60, 255, 255, 255);

    public WaveformControl()
    {
        _visuals.Add(_visual);
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        ClipToBounds = true;
        Focusable = true;

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        MouseMove += OnMouseMove;
        SizeChanged += (_, _) => Redraw();

        // Suppress default context menu so right-drag selection works reliably
        ContextMenuOpening += (s, e) => e.Handled = true;
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    public void Redraw()
    {
        var dc = _visual.RenderOpen();
        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0)
        {
            dc.Close();
            return;
        }

        // Background
        dc.DrawRectangle(new SolidColorBrush(BgColor), null, new Rect(0, 0, width, height));

        var viewDuration = ViewEndMs - ViewStartMs;
        if (viewDuration <= 0) { dc.Close(); return; }

        double midY = height / 2;
        double amplitude = height * 0.42;

        // Draw time grid
        DrawGrid(dc, width, height, viewDuration);

        // Draw center line
        dc.DrawLine(new Pen(new SolidColorBrush(CenterLineColor), 1),
            new Point(0, midY), new Point(width, midY));

        // Draw selection region with edge handles
        if (SelectionStartMs >= 0 && SelectionEndMs >= 0 && SelectionEndMs != SelectionStartMs)
        {
            double x1 = MsToX(Math.Min(SelectionStartMs, SelectionEndMs), width);
            double x2 = MsToX(Math.Max(SelectionStartMs, SelectionEndMs), width);
            dc.DrawRectangle(new SolidColorBrush(SelectionColor), null,
                new Rect(x1, 0, x2 - x1, height));

            // Draw edge handles (vertical bars with grip)
            var handleBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            var handlePen = new Pen(handleBrush, 2);
            dc.DrawLine(handlePen, new Point(x1, 0), new Point(x1, height));
            dc.DrawLine(handlePen, new Point(x2, 0), new Point(x2, height));

            // Draw grip triangles at center of handles
            double gripY = height / 2;
            dc.DrawRectangle(handleBrush, null, new Rect(x1 - 3, gripY - 10, 6, 20));
            dc.DrawRectangle(handleBrush, null, new Rect(x2 - 3, gripY - 10, 6, 20));
        }

        // Draw waveform
        if (WaveformService != null && WaveformService.PeakCount > 0)
        {
            var peaks = WaveformService.GetPeaksForView(ViewStartMs, ViewEndMs, (int)width);
            var wavePen = new Pen(new SolidColorBrush(WaveColor), 1);
            var fillBrush = new SolidColorBrush(WaveFillColor);

            // Build filled polygon
            var topPoints = new List<Point>();
            var bottomPoints = new List<Point>();

            for (int i = 0; i < peaks.Length; i++)
            {
                double x = i;
                double yMax = midY - peaks[i].max * amplitude;
                double yMin = midY - peaks[i].min * amplitude;
                topPoints.Add(new Point(x, yMax));
                bottomPoints.Add(new Point(x, yMin));
            }

            // Draw filled area
            if (topPoints.Count > 1)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(topPoints[0], true, true);
                    for (int i = 1; i < topPoints.Count; i++)
                        ctx.LineTo(topPoints[i], true, false);
                    for (int i = bottomPoints.Count - 1; i >= 0; i--)
                        ctx.LineTo(bottomPoints[i], true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(fillBrush, null, geometry);

                // Draw outline
                var topGeom = new StreamGeometry();
                using (var ctx = topGeom.Open())
                {
                    ctx.BeginFigure(topPoints[0], false, false);
                    for (int i = 1; i < topPoints.Count; i++)
                        ctx.LineTo(topPoints[i], true, false);
                }
                topGeom.Freeze();
                dc.DrawGeometry(null, wavePen, topGeom);

                var botGeom = new StreamGeometry();
                using (var ctx = botGeom.Open())
                {
                    ctx.BeginFigure(bottomPoints[0], false, false);
                    for (int i = 1; i < bottomPoints.Count; i++)
                        ctx.LineTo(bottomPoints[i], true, false);
                }
                botGeom.Freeze();
                dc.DrawGeometry(null, wavePen, botGeom);
            }
        }

        // Draw markers
        if (Markers != null)
        {
            var markerPen = new Pen(new SolidColorBrush(MarkerColor), 2);
            var markerBrush = new SolidColorBrush(MarkerColor);
            var typeface = new Typeface("Segoe UI");

            foreach (var marker in Markers)
            {
                double x = MsToX(marker.TimestampMs, width);
                if (x < 0 || x > width) continue;

                dc.DrawLine(markerPen, new Point(x, 0), new Point(x, height));

                // Label
                var text = new FormattedText(
                    marker.Name, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, 10, markerBrush, 96);
                dc.DrawText(text, new Point(x + 3, 2));
            }
        }

        // Draw playback cursor
        if (PlaybackPositionMs >= 0)
        {
            double x = MsToX(PlaybackPositionMs, width);
            if (x >= 0 && x <= width)
            {
                var cursorPen = new Pen(new SolidColorBrush(CursorColor), 2);
                dc.DrawLine(cursorPen, new Point(x, 0), new Point(x, height));
            }
        }

        dc.Close();
    }

    private void DrawGrid(DrawingContext dc, double width, double height, double viewDuration)
    {
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);
        var typeface = new Typeface("Segoe UI");
        var textBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));

        // Choose grid interval based on zoom
        double[] intervals = { 100, 250, 500, 1000, 2000, 5000, 10000, 30000, 60000, 120000, 300000 };
        double interval = intervals[0];
        foreach (var iv in intervals)
        {
            if (viewDuration / iv <= 12) { interval = iv; break; }
            interval = iv;
        }

        double startGrid = Math.Ceiling(ViewStartMs / interval) * interval;
        for (double t = startGrid; t <= ViewEndMs; t += interval)
        {
            double x = MsToX(t, width);
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, height));

            var label = FormatTimeLabel(t);
            var text = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 9, textBrush, 96);
            dc.DrawText(text, new Point(x + 2, height - 14));
        }
    }

    private double MsToX(double ms, double width)
    {
        var viewDuration = ViewEndMs - ViewStartMs;
        if (viewDuration <= 0) return 0;
        return (ms - ViewStartMs) / viewDuration * width;
    }

    private double XToMs(double x, double width)
    {
        var viewDuration = ViewEndMs - ViewStartMs;
        return ViewStartMs + x / width * viewDuration;
    }

    private static string FormatTimeLabel(double ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.TotalMinutes >= 1
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
            : $"{ts.Seconds}.{ts.Milliseconds / 1000:D1}s";
    }

    // --- Mouse Interaction ---

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var pos = e.GetPosition(this);
        double centerMs = XToMs(pos.X, ActualWidth);
        double viewDuration = ViewEndMs - ViewStartMs;

        double factor = e.Delta > 0 ? 0.7 : 1.4;
        double newDuration = Math.Max(500, Math.Min(TotalDurationMs(), viewDuration * factor));

        double ratio = (centerMs - ViewStartMs) / viewDuration;
        ViewStartMs = centerMs - newDuration * ratio;
        ViewEndMs = ViewStartMs + newDuration;

        ClampView();
        ViewChanged?.Invoke(ViewStartMs, ViewEndMs);
        Redraw();
    }

    // --- Left button: click = seek, drag = pan ---

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        CaptureMouse();
        _dragStart = pos;
        _dragStartViewMs = ViewStartMs;
        _leftDown = true;
        _leftDragConfirmed = false;
        _isPanning = false;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_leftDown && !_leftDragConfirmed)
        {
            // It was a click (no significant drag) → seek to position
            var pos = e.GetPosition(this);
            double ms = XToMs(pos.X, ActualWidth);
            SeekRequested?.Invoke(ms);
        }

        _leftDown = false;
        _leftDragConfirmed = false;
        _isPanning = false;
        ReleaseMouseCapture();
        Redraw();
    }

    // --- Right button: drag = region selection, with resize handles ---

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        CaptureMouse();
        _dragStart = pos;

        // Check if clicking on a selection resize handle
        if (SelectionStartMs >= 0 && SelectionEndMs > SelectionStartMs)
        {
            double startX = MsToX(SelectionStartMs, ActualWidth);
            double endX = MsToX(SelectionEndMs, ActualWidth);

            if (Math.Abs(pos.X - startX) <= HandleWidth)
            {
                _isResizingLeft = true;
                return;
            }
            if (Math.Abs(pos.X - endX) <= HandleWidth)
            {
                _isResizingRight = true;
                return;
            }
        }

        // Start a new selection
        _isSelecting = true;
        SelectionStartMs = (long)XToMs(pos.X, ActualWidth);
        SelectionEndMs = SelectionStartMs;
        Redraw();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);

        if (_isSelecting)
        {
            SelectionEndMs = (long)XToMs(pos.X, ActualWidth);
            if (SelectionEndMs < SelectionStartMs)
                (SelectionStartMs, SelectionEndMs) = (SelectionEndMs, SelectionStartMs);

            // If selection is too small (< 50ms), discard it
            if (SelectionEndMs - SelectionStartMs < 50)
            {
                SelectionStartMs = -1;
                SelectionEndMs = -1;
            }
            else
            {
                SelectionChanged?.Invoke(SelectionStartMs, SelectionEndMs);
            }
        }
        else if (_isResizingLeft || _isResizingRight)
        {
            if (SelectionEndMs < SelectionStartMs)
                (SelectionStartMs, SelectionEndMs) = (SelectionEndMs, SelectionStartMs);
            SelectionChanged?.Invoke(SelectionStartMs, SelectionEndMs);
        }

        _isSelecting = false;
        _isResizingLeft = false;
        _isResizingRight = false;
        ReleaseMouseCapture();
        Redraw();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);

        // --- Left button: pan or confirm drag ---
        if (e.LeftButton == MouseButtonState.Pressed && _leftDown)
        {
            if (!_leftDragConfirmed)
            {
                double dist = Math.Abs(pos.X - _dragStart.X);
                if (dist >= DragThreshold)
                {
                    _leftDragConfirmed = true;
                    _isPanning = true;
                }
            }

            if (_isPanning)
            {
                double dxMs = (pos.X - _dragStart.X) / ActualWidth * (ViewEndMs - ViewStartMs);
                double viewDuration = ViewEndMs - ViewStartMs;
                ViewStartMs = _dragStartViewMs - dxMs;
                ViewEndMs = ViewStartMs + viewDuration;
                ClampView();
                ViewChanged?.Invoke(ViewStartMs, ViewEndMs);
                Redraw();
            }
            return;
        }

        // --- Right button: selection / resize ---
        if (e.RightButton == MouseButtonState.Pressed)
        {
            if (_isResizingLeft)
            {
                SelectionStartMs = (long)XToMs(pos.X, ActualWidth);
                long s = Math.Min(SelectionStartMs, SelectionEndMs);
                long en = Math.Max(SelectionStartMs, SelectionEndMs);
                SelectionChanged?.Invoke(s, en);
                Redraw();
            }
            else if (_isResizingRight)
            {
                SelectionEndMs = (long)XToMs(pos.X, ActualWidth);
                long s = Math.Min(SelectionStartMs, SelectionEndMs);
                long en = Math.Max(SelectionStartMs, SelectionEndMs);
                SelectionChanged?.Invoke(s, en);
                Redraw();
            }
            else if (_isSelecting)
            {
                SelectionEndMs = (long)XToMs(pos.X, ActualWidth);
                long s = Math.Min(SelectionStartMs, SelectionEndMs);
                long en = Math.Max(SelectionStartMs, SelectionEndMs);
                SelectionChanged?.Invoke(s, en);
                Redraw();
            }
            return;
        }

        // --- Hover: update cursor ---
        if (SelectionStartMs >= 0 && SelectionEndMs > SelectionStartMs)
        {
            double startX = MsToX(SelectionStartMs, ActualWidth);
            double endX = MsToX(SelectionEndMs, ActualWidth);
            if (Math.Abs(pos.X - startX) <= HandleWidth || Math.Abs(pos.X - endX) <= HandleWidth)
                Cursor = Cursors.SizeWE;
            else
                Cursor = Cursors.Cross;
        }
        else
        {
            Cursor = Cursors.Cross;
        }
    }

    private void ClampView()
    {
        double total = TotalDurationMs();
        if (total <= 0) total = 60000;
        double duration = ViewEndMs - ViewStartMs;

        if (ViewStartMs < 0) { ViewStartMs = 0; ViewEndMs = duration; }
        if (ViewEndMs > total) { ViewEndMs = total; ViewStartMs = total - duration; }
        if (ViewStartMs < 0) ViewStartMs = 0;
    }

    private double TotalDurationMs()
    {
        return WaveformService?.TotalDurationMs ?? 0;
    }
}
