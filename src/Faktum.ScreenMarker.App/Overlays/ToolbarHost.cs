using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.App.Toolbar;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Monitors;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace Faktum.ScreenMarker.App.Overlays;

internal sealed class ToolbarHost : Grid
{
    private ToolbarControl? _toolbar;
    private MonitorInfo _monitor;
    private SettingsPersistenceCoordinator? _persistence;
    private bool _dragging;
    private WpfPoint _dragStart;
    private Thickness _dragStartMargin;

    public ToolbarHost(MonitorInfo monitor)
    {
        _monitor = monitor;
        System.Windows.Controls.Panel.SetZIndex(this, OverlayLayerZIndex.ToolbarHost);
        HorizontalAlignment = WpfHorizontalAlignment.Left;
        VerticalAlignment = WpfVerticalAlignment.Top;
        Background = null;
    }

    internal ToolbarControl? Toolbar => _toolbar;

    internal void Attach(
        ToolbarControl toolbar,
        AppSettings settings,
        SettingsPersistenceCoordinator persistence,
        MonitorInfo monitor)
    {
        Detach();
        _toolbar = toolbar;
        _monitor = monitor;
        _persistence = persistence;
        Children.Add(toolbar);
        toolbar.Loaded += OnToolbarLoaded;
        toolbar.MouseLeftButtonDown += OnToolbarMouseLeftButtonDown;
        toolbar.MouseLeftButtonUp += OnToolbarMouseLeftButtonUp;
        toolbar.MouseMove += OnToolbarMouseMove;
        toolbar.LostMouseCapture += OnToolbarLostMouseCapture;
        toolbar.EnsureMeasured();

        var (screenLeft, screenTop) = ToolbarPlacementHelper.ResolveInitialPlacement(settings, monitor);
        var dipOriginX = monitor.Left / monitor.DipScaleX;
        var dipOriginY = monitor.Top / monitor.DipScaleY;
        ApplyLocalPlacement(screenLeft - dipOriginX, screenTop - dipOriginY);
    }

    internal void Detach()
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.Loaded -= OnToolbarLoaded;
        _toolbar.MouseLeftButtonDown -= OnToolbarMouseLeftButtonDown;
        _toolbar.MouseLeftButtonUp -= OnToolbarMouseLeftButtonUp;
        _toolbar.MouseMove -= OnToolbarMouseMove;
        _toolbar.LostMouseCapture -= OnToolbarLostMouseCapture;
        Children.Remove(_toolbar);
        _toolbar = null;
        _persistence = null;
        _dragging = false;
        Margin = new Thickness(0);
    }

    internal void RestoreLocalPlacement(double localLeft, double localTop) => ApplyLocalPlacement(localLeft, localTop);

    internal bool TryGetLocalPlacement(out double localLeft, out double localTop)
    {
        localLeft = Margin.Left;
        localTop = Margin.Top;
        return _toolbar is not null;
    }

    internal void UpdateMonitor(MonitorInfo monitor) => _monitor = monitor;

    private void OnToolbarLoaded(object sender, RoutedEventArgs e)
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.EnsureMeasured();
        UpdateLayout();
        ClampToolbarPosition();
    }

    private void OnToolbarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_toolbar is null || sender is not ToolbarControl)
        {
            return;
        }

        _dragging = true;
        _dragStart = e.GetPosition(this);
        _dragStartMargin = Margin;
        _toolbar.CaptureMouse();
        e.Handled = true;
    }

    private void OnToolbarMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_dragging || _toolbar is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - _dragStart;
        ApplyLocalPlacement(_dragStartMargin.Left + delta.X, _dragStartMargin.Top + delta.Y);
        e.Handled = true;
    }

    private void OnToolbarMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging || _toolbar is null)
        {
            return;
        }

        _dragging = false;
        _toolbar.ReleaseMouseCapture();
        PersistPlacementDebounced();
        e.Handled = true;
    }

    private void OnToolbarLostMouseCapture(object sender, WpfMouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        PersistPlacementDebounced();
    }

    private void ApplyLocalPlacement(double localLeft, double localTop)
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbar.EnsureMeasured();
        var clamped = ClampToMonitor(localLeft, localTop);
        Margin = new Thickness(clamped.Left, clamped.Top, 0, 0);
        UpdateLayout();
    }

    private void ClampToolbarPosition()
    {
        if (_toolbar is null)
        {
            return;
        }

        ApplyLocalPlacement(Margin.Left, Margin.Top);
    }

    private (double Left, double Top) ClampToMonitor(double localLeft, double localTop)
    {
        var parentWidth = Parent is FrameworkElement parent && parent.ActualWidth > 0
            ? parent.ActualWidth
            : _monitor.Width / _monitor.DipScaleX;
        var parentHeight = Parent is FrameworkElement parentElement && parentElement.ActualHeight > 0
            ? parentElement.ActualHeight
            : _monitor.Height / _monitor.DipScaleY;
        _toolbar!.EnsureMeasured();
        var toolbarWidth = _toolbar.ActualWidth > 0 ? _toolbar.ActualWidth : _toolbar.Width;
        var toolbarHeight = _toolbar.ActualHeight > 0 ? _toolbar.ActualHeight : _toolbar.Height;
        if (toolbarWidth <= 0)
        {
            toolbarWidth = 400;
        }

        if (toolbarHeight <= 0)
        {
            toolbarHeight = 48;
        }

        var left = Math.Clamp(localLeft, 0, Math.Max(0, parentWidth - toolbarWidth));
        var top = Math.Clamp(localTop, 0, Math.Max(0, parentHeight - toolbarHeight));
        return (left, top);
    }

    private void PersistPlacementDebounced()
    {
        if (_persistence is null || _toolbar is null)
        {
            return;
        }

        var dipOriginX = _monitor.Left / _monitor.DipScaleX;
        var dipOriginY = _monitor.Top / _monitor.DipScaleY;
        var placement = ToolbarPlacementHelper.CreatePersistedPlacement(
            _monitor,
            dipOriginX + Margin.Left,
            dipOriginY + Margin.Top);
        _persistence.UpdateToolbarPlacement(placement);
    }
}
