using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Windowing;

public static class WindowPositionHelper
{
    public static bool SetTopmostPosition(nint windowHandle, int physicalX, int physicalY)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        var success = NativeMethods.SetWindowPos(
            windowHandle,
            NativeMethods.HwndTopmost,
            physicalX,
            physicalY,
            0,
            0,
            NativeMethods.SwpNosize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        if (!success)
        {
            DiagnosticLog.Write("WindowPosition", "SetWindowPos failed.");
        }

        return success;
    }

    /// <summary>
    /// Raises the window into the topmost Z-order band without changing position or size.
    /// Does not use another window as hWndInsertAfter — overlay HWND must never be passed here.
    /// </summary>
    public static bool EnsureTopmostBand(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        var success = NativeMethods.SetWindowPos(
            windowHandle,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        if (!success)
        {
            DiagnosticLog.Write("WindowPosition", "EnsureTopmostBand failed.");
        }

        return success;
    }

    public static bool TryGetWindowRect(nint windowHandle, out NativeWindowRect bounds)
    {
        bounds = default;
        if (windowHandle == 0)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(windowHandle, out var nativeRect))
        {
            return false;
        }

        bounds = new NativeWindowRect(nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
        return true;
    }
}
