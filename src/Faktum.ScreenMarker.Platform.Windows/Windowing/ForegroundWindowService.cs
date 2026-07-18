using System.Runtime.InteropServices;
using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Windowing;

public sealed class ForegroundWindowService
{
    private nint _capturedWindow;

    public void Capture()
    {
        _capturedWindow = NativeMethods.GetForegroundWindow();
    }

    public void RestoreBestEffort()
    {
        if (_capturedWindow == 0 || !NativeMethods.IsWindow(_capturedWindow))
        {
            return;
        }

        _ = NativeMethods.ShowWindow(_capturedWindow, NativeMethods.SwShow);
        _ = NativeMethods.SetForegroundWindow(_capturedWindow);
        _capturedWindow = 0;
    }
}

public static class WindowStyles
{
    public const int GwlExstyle = -20;
    public const int WsExToolwindow = 0x00000080;
    public const int WsExNoactivate = 0x08000000;
    public const int WsExTransparent = 0x00000020;
    public const int WsExLayered = 0x00080000;
    public const int WsExTopmost = 0x00000008;

    public static nint GetWindowLongPtr(nint hWnd, int nIndex) =>
        NativeWindowStyleMethods.GetWindowLongPtr(hWnd, nIndex);

    public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong) =>
        NativeWindowStyleMethods.SetWindowLongPtr(hWnd, nIndex, dwNewLong);
}

internal static partial class NativeWindowStyleMethods
{
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
