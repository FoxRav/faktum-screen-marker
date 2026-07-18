using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Geometry;
using Faktum.ScreenMarker.Core.Interaction;
using Faktum.ScreenMarker.Core.Simplification;

namespace Faktum.ScreenMarker.App.Interaction;

internal sealed class OverlayPointerInputController
{
    private readonly DrawingSession _session;
    private readonly List<Point2D> _livePoints = [];
    private Point2D _startPoint;
    private DrawingTool? _interactionTool;
    private StrokeStyle _latchedStyle = StrokeStyle.DefaultPen;
    private string _monitorDeviceName = string.Empty;
    private readonly HashSet<int> _eraserRemovedIds = [];

    public OverlayPointerInputController(DrawingSession session, string monitorDeviceName)
    {
        _session = session;
        _monitorDeviceName = monitorDeviceName;
    }

    public OverlayPointerInputController(DrawingSession session)
        : this(session, string.Empty)
    {
    }

    public bool IsDrawing { get; private set; }

    internal DrawingTool? LatchedInteractionTool => _interactionTool;

    internal int LivePointCount => _livePoints.Count;

    public void BeginInteraction(Point2D point, PointerInteractionLatch latch, Action<Point2D> showTextEditor)
    {
        _interactionTool = latch.Tool;
        _latchedStyle = latch.Style;
        _monitorDeviceName = latch.MonitorDeviceName;
        _startPoint = point;
        _livePoints.Clear();
        _eraserRemovedIds.Clear();
        IsDrawing = true;

        switch (latch.Tool)
        {
            case DrawingTool.Pen:
                _livePoints.Add(point);
                _session.BeginPreview(_monitorDeviceName, new FreehandStroke(_session.AllocateId(), _monitorDeviceName, _latchedStyle, _livePoints.ToArray()));
                break;
            case DrawingTool.Line:
                _session.BeginPreview(_monitorDeviceName, new LineAnnotation(_session.AllocateId(), _monitorDeviceName, _latchedStyle, point, point));
                break;
            case DrawingTool.Arrow:
                _session.BeginPreview(_monitorDeviceName, new ArrowAnnotation(_session.AllocateId(), _monitorDeviceName, _latchedStyle, point, point));
                break;
            case DrawingTool.Rectangle:
                _session.BeginPreview(_monitorDeviceName, new RectangleAnnotation(_session.AllocateId(), _monitorDeviceName, _latchedStyle, new Rect2D(point.X, point.Y, 0, 0)));
                break;
            case DrawingTool.Ellipse:
                _session.BeginPreview(_monitorDeviceName, new EllipseAnnotation(_session.AllocateId(), _monitorDeviceName, _latchedStyle, new Rect2D(point.X, point.Y, 0, 0)));
                break;
            case DrawingTool.Text:
                IsDrawing = false;
                showTextEditor(point);
                break;
            case DrawingTool.Eraser:
                TryEraseAt(point);
                break;
        }
    }

    public void BeginLeftButtonDrag(Point2D point, DrawingTool tool, Action<Point2D> showTextEditor)
    {
        BeginInteraction(point, new PointerInteractionLatch(tool, _session.ActiveStyle, _monitorDeviceName), showTextEditor);
    }

    public void ConfigureMonitor(string monitorDeviceName) => _monitorDeviceName = monitorDeviceName;

    public void UpdateDrag(Point2D point, bool shift)
    {
        if (!IsDrawing || _interactionTool is not DrawingTool activeTool)
        {
            return;
        }

        switch (activeTool)
        {
            case DrawingTool.Pen:
                if (!DrawingValidation.IsValidPoint(point) || _livePoints.Count >= FreehandInputValidation.MaxLivePoints)
                {
                    return;
                }

                if (_livePoints.Count > 0 && GeometryHelpers.ShouldIgnoreJitter(_livePoints[^1], point))
                {
                    return;
                }

                _livePoints.Add(point);
                _session.UpdatePreview(new FreehandStroke(_session.PreviewObject!.Id, _monitorDeviceName, _latchedStyle, _livePoints.ToArray()));
                break;
            case DrawingTool.Line:
            {
                var (start, end) = shift ? GeometryHelpers.ConstrainLine(_startPoint, point) : (_startPoint, point);
                _session.UpdatePreview(new LineAnnotation(_session.PreviewObject!.Id, _monitorDeviceName, _latchedStyle, start, end));
                break;
            }
            case DrawingTool.Arrow:
            {
                var (start, end) = shift ? GeometryHelpers.ConstrainLine(_startPoint, point) : (_startPoint, point);
                _session.UpdatePreview(new ArrowAnnotation(_session.PreviewObject!.Id, _monitorDeviceName, _latchedStyle, start, end));
                break;
            }
            case DrawingTool.Rectangle:
                _session.UpdatePreview(new RectangleAnnotation(_session.PreviewObject!.Id, _monitorDeviceName, _latchedStyle, GeometryHelpers.NormalizeRect(_startPoint, point, shift)));
                break;
            case DrawingTool.Ellipse:
                _session.UpdatePreview(new EllipseAnnotation(_session.PreviewObject!.Id, _monitorDeviceName, _latchedStyle, GeometryHelpers.NormalizeRect(_startPoint, point, shift)));
                break;
            case DrawingTool.Eraser:
                TryEraseAt(point);
                break;
        }
    }

    public bool CompleteDrag(DrawingSession session)
    {
        if (!IsDrawing)
        {
            return false;
        }

        if (_interactionTool == DrawingTool.Eraser)
        {
            ResetAfterCompletion();
            return false;
        }

        try
        {
            return session.CommitPreview();
        }
        finally
        {
            ResetAfterCompletion();
        }
    }

    public void CancelDrawing(DrawingSession session)
    {
        if (!IsDrawing)
        {
            return;
        }

        if (_interactionTool != DrawingTool.Eraser)
        {
            session.CancelPreview();
        }

        ResetAfterCompletion();
    }

    public bool TryEraseAt(Point2D point) => _session.TryEraseAt(_monitorDeviceName, point, EraserSettings.HitRadiusDips, _eraserRemovedIds);

    private void ResetAfterCompletion()
    {
        IsDrawing = false;
        _interactionTool = null;
        _livePoints.Clear();
        _eraserRemovedIds.Clear();
        _startPoint = default;
    }
}
