using System.Windows;
using System.Windows.Media;
using Faktum.ScreenMarker.Core.Drawing;
using WpfSize = System.Windows.Size;

namespace Faktum.ScreenMarker.App.Drawing;

public sealed class DrawingSurface : FrameworkElement
{
    private readonly string _monitorDeviceName;
    private readonly DrawingSession _session;
    private readonly DrawingRenderer _renderer = new();
    private readonly VisualCollection _visuals;
    private double _pixelsPerDip = 1.0;

    public DrawingSurface(string monitorDeviceName, DrawingSession session)
    {
        _monitorDeviceName = monitorDeviceName;
        _session = session;
        _visuals = new VisualCollection(this);
        _visuals.Add(_renderer.CommittedVisual);
        _visuals.Add(_renderer.PreviewVisual);
        Focusable = true;
        _session.Changed += OnSessionChanged;
        Unloaded += (_, _) => _session.Changed -= OnSessionChanged;
        Loaded += (_, _) => RefreshDpiAndRender();
        InvalidateRender();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RefreshDpiAndRender();
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override WpfSize MeasureOverride(WpfSize availableSize) => availableSize;

    protected override WpfSize ArrangeOverride(WpfSize finalSize) => finalSize;

    private void OnSessionChanged() => InvalidateRender();

    private void RefreshDpiAndRender()
    {
        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        InvalidateRender();
    }

    public void InvalidateRender()
    {
        var committed = _session.GetObjectsForMonitor(_monitorDeviceName);
        DrawingObject? preview = null;
        if (_session.PreviewObject is not null &&
            string.Equals(_session.PreviewObject.MonitorDeviceName, _monitorDeviceName, StringComparison.Ordinal))
        {
            preview = _session.PreviewObject;
        }

        _renderer.Render(committed, preview, _pixelsPerDip);
    }
}
