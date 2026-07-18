using Faktum.ScreenMarker.Platform.Windows.Monitors;
using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Windowing;

public static class OverlayWindowPlacement
{
    public static void ApplyMonitorBounds(nint windowHandle, MonitorInfo monitor)
    {
        if (windowHandle == 0)
        {
            return;
        }

        _ = NativeMethods.SetWindowPos(
            windowHandle,
            NativeMethods.HwndTopmost,
            monitor.Left,
            monitor.Top,
            monitor.Width,
            monitor.Height,
            NativeMethods.SwpShowWindow);
    }
}
