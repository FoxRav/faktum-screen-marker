using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Monitors;
using Faktum.ScreenMarker.Platform.Windows.Windowing;

namespace Faktum.ScreenMarker.App.Toolbar;

public static class ToolbarPlacementHelper
{
    public static (double Left, double Top) ResolveInitialPlacement(AppSettings settings, MonitorInfo monitor)
    {
        var dipOriginX = monitor.Left / monitor.DipScaleX;
        var dipOriginY = monitor.Top / monitor.DipScaleY;
        var placement = settings.ToolbarPlacement;

        if (placement is not null && placement.Value.MonitorDeviceName == monitor.DeviceName)
        {
            return ClampToMonitor(monitor, dipOriginX + placement.Value.Left, dipOriginY + placement.Value.Top);
        }

        var monitors = MonitorEnumerator.EnumerateActiveMonitors();
        if (placement is not null && monitors.All(m => m.DeviceName != placement.Value.MonitorDeviceName))
        {
            return ClampToMonitor(monitor, dipOriginX + (monitor.Width / monitor.DipScaleX / 2.0) - 200.0, dipOriginY + 24.0);
        }

        return ClampToMonitor(monitor, dipOriginX + (monitor.Width / monitor.DipScaleX / 2.0) - 200.0, dipOriginY + 24.0);
    }

    public static ToolbarPlacement CreatePersistedPlacement(MonitorInfo monitor, double windowLeft, double windowTop)
    {
        var dipOriginX = monitor.Left / monitor.DipScaleX;
        var dipOriginY = monitor.Top / monitor.DipScaleY;
        return new ToolbarPlacement(monitor.DeviceName, windowLeft - dipOriginX, windowTop - dipOriginY);
    }

    public static void ApplyPositionAfterSourceInit(nint windowHandle, MonitorInfo monitor, double dipLeft, double dipTop)
    {
        if (windowHandle == 0)
        {
            return;
        }

        var physicalLeft = monitor.Left + (int)Math.Round(dipLeft * monitor.DipScaleX);
        var physicalTop = monitor.Top + (int)Math.Round(dipTop * monitor.DipScaleY);
        WindowPositionHelper.SetTopmostPosition(windowHandle, physicalLeft, physicalTop);
    }

    private static (double Left, double Top) ClampToMonitor(MonitorInfo monitor, double dipLeft, double dipTop)
    {
        var dipOriginX = monitor.Left / monitor.DipScaleX;
        var dipOriginY = monitor.Top / monitor.DipScaleY;
        var dipWidth = monitor.Width / monitor.DipScaleX;
        var dipHeight = monitor.Height / monitor.DipScaleY;
        const double toolbarWidth = 400.0;
        const double toolbarHeight = 48.0;
        var left = Math.Clamp(dipLeft, dipOriginX, dipOriginX + Math.Max(0, dipWidth - toolbarWidth));
        var top = Math.Clamp(dipTop, dipOriginY, dipOriginY + Math.Max(0, dipHeight - toolbarHeight));
        return (left, top);
    }
}
