using Faktum.ScreenMarker.Core.Geometry;
using Faktum.ScreenMarker.Core.History;
using Faktum.ScreenMarker.Core.Interaction;
using Faktum.ScreenMarker.Core.Simplification;

namespace Faktum.ScreenMarker.Core.Drawing;

public sealed class DrawingSession : IDisposable
{
    private readonly List<DrawingObject> _objects = [];
    private readonly DrawingHistory _history = new();
    private int _nextId = 1;
    private bool _disposed;

    public event Action? Changed;

    public DrawingTool ActiveTool { get; set; } = DrawingTool.Pen;

    public StrokeStyle ActiveStyle { get; set; } = StrokeStyle.DefaultPen;

    public IReadOnlyList<DrawingObject> Objects => _objects;

    public DrawingHistory History => _history;

    public DrawingObject? PreviewObject { get; private set; }

    public string? PreviewMonitorDeviceName { get; private set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
    }

    public IReadOnlyList<DrawingObject> GetObjectsForMonitor(string monitorDeviceName) =>
        _objects.Where(o => string.Equals(o.MonitorDeviceName, monitorDeviceName, StringComparison.Ordinal)).ToArray();

    public void Clear()
    {
        _objects.Clear();
        _history.Clear();
        PreviewObject = null;
        PreviewMonitorDeviceName = null;
        _nextId = 1;
        NotifyChanged();
    }

    public void BeginPreview(string monitorDeviceName, DrawingObject preview)
    {
        PreviewMonitorDeviceName = monitorDeviceName;
        PreviewObject = preview;
        NotifyChanged();
    }

    public void UpdatePreview(DrawingObject preview)
    {
        PreviewObject = preview;
        NotifyChanged();
    }

    public void CancelPreview()
    {
        PreviewObject = null;
        PreviewMonitorDeviceName = null;
        NotifyChanged();
    }

    public bool CommitPreview()
    {
        if (PreviewObject is null)
        {
            return false;
        }

        var committed = NormalizeForCommit(PreviewObject);
        if (committed is null)
        {
            PreviewObject = null;
            PreviewMonitorDeviceName = null;
            NotifyChanged();
            return false;
        }

        _history.Execute(new AddObjectCommand(committed, _objects.Count), this);
        PreviewObject = null;
        PreviewMonitorDeviceName = null;
        NotifyChanged();
        return true;
    }

    public bool EraseAt(string monitorDeviceName, Point2D point) =>
        TryEraseAt(monitorDeviceName, point, EraserSettings.HitRadiusDips, excludedIds: null);

    public bool TryEraseAt(
        string monitorDeviceName,
        Point2D point,
        double hitRadiusDips,
        ISet<int>? excludedIds)
    {
        var monitorObjects = GetObjectsForMonitor(monitorDeviceName);
        var id = HitTesting.FindTopmostHit(monitorObjects, point, hitRadiusDips);
        if (id is null || excludedIds?.Contains(id.Value) == true)
        {
            return false;
        }

        var index = _objects.FindIndex(o => o.Id == id.Value);
        if (index < 0)
        {
            return false;
        }

        var target = _objects[index];
        _history.Execute(new RemoveObjectCommand(target, index), this);
        excludedIds?.Add(id.Value);
        NotifyChanged();
        return true;
    }

    public void ClearAllWithHistory()
    {
        if (_objects.Count == 0)
        {
            return;
        }

        var snapshot = _objects.ToArray();
        _history.Execute(new ClearAllCommand(snapshot), this);
        NotifyChanged();
    }

    public void Undo()
    {
        if (!_history.CanUndo)
        {
            return;
        }

        _history.Undo(this);
        NotifyChanged();
    }

    public void Redo()
    {
        if (!_history.CanRedo)
        {
            return;
        }

        _history.Redo(this);
        NotifyChanged();
    }

    internal void AddObjectInternal(DrawingObject obj, int insertionIndex)
    {
        insertionIndex = Math.Clamp(insertionIndex, 0, _objects.Count);
        _objects.Insert(insertionIndex, obj);
    }

    internal void RemoveObjectInternal(int id)
    {
        _objects.RemoveAll(o => o.Id == id);
    }

    internal void ClearObjectsInternal()
    {
        _objects.Clear();
        PreviewObject = null;
        PreviewMonitorDeviceName = null;
    }

    public int AllocateId() => _nextId++;

    private void NotifyChanged() => Changed?.Invoke();

    private static DrawingObject? NormalizeForCommit(DrawingObject obj) =>
        obj switch
        {
            FreehandStroke stroke when FreehandInputValidation.TryPrepareForCommit(stroke.Points, out var prepared) =>
                stroke with { Points = RamerDouglasPeucker.Simplify(prepared) },
            FreehandStroke => null,
            LineAnnotation line when line.Start.DistanceTo(line.End) > 0.5 => line,
            ArrowAnnotation arrow when arrow.Start.DistanceTo(arrow.End) > 0.5 => arrow,
            RectangleAnnotation rect when rect.Bounds.Width > 0.5 && rect.Bounds.Height > 0.5 => rect,
            EllipseAnnotation ellipse when ellipse.Bounds.Width > 0.5 && ellipse.Bounds.Height > 0.5 => ellipse,
            TextAnnotation text when DrawingValidation.IsValidText(text.Text) => text,
            _ => null,
        };
}
