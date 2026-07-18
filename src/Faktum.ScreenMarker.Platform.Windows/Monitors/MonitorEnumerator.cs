using System.Runtime.InteropServices;
using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Monitors;

public readonly record struct MonitorInfo(
    string DeviceName,
    int Left,
    int Top,
    int Width,
    int Height,
    double DpiX,
    double DpiY)
{
    public double DipScaleX => DpiX / 96.0;
    public double DipScaleY => DpiY / 96.0;
}

public static class MonitorEnumerator
{
    private static readonly List<MonitorInfo> Buffer = [];

    public static IReadOnlyList<MonitorInfo> EnumerateActiveMonitors()
    {
        Buffer.Clear();
        NativeMonitorMethods.MonitorEnumProc callback = (hMonitor, _, ref rect, _) =>
        {
            var info = new NativeMonitorMethods.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<NativeMonitorMethods.MONITORINFOEX>(),
            };
            if (!NativeMonitorMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            var dpiX = 96.0;
            var dpiY = 96.0;
            if (NativeMonitorMethods.GetDpiForMonitor(hMonitor, NativeMonitorMethods.MonitorDpiType.Effective, out var dx, out var dy) == 0)
            {
                dpiX = dx;
                dpiY = dy;
            }

            Buffer.Add(new MonitorInfo(
                info.szDevice.TrimEnd('\0'),
                info.rcMonitor.Left,
                info.rcMonitor.Top,
                info.rcMonitor.Right - info.rcMonitor.Left,
                info.rcMonitor.Bottom - info.rcMonitor.Top,
                dpiX,
                dpiY));
            return true;
        };

        _ = NativeMonitorMethods.EnumDisplayMonitors(0, nint.Zero, callback, nint.Zero);
        return Buffer.ToArray();
    }
}

public static class MonitorConversion
{
    public static (double X, double Y) PixelsToDips(MonitorInfo monitor, double pixelX, double pixelY) =>
        (pixelX / monitor.DipScaleX, pixelY / monitor.DipScaleY);

    public static (double X, double Y) DipsToPixels(MonitorInfo monitor, double dipX, double dipY) =>
        (dipX * monitor.DipScaleX, dipY * monitor.DipScaleY);
}
