using System.Runtime.InteropServices;
using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Windowing;

public readonly record struct OverlayExtendedStyleState(
    long ExtendedStyle,
    bool HasTransparent,
    bool HasNoActivate,
    bool HasToolWindow,
    bool HasTopmost)
{
    public static OverlayExtendedStyleState FromExtendedStyle(long extendedStyle) =>
        new(
            extendedStyle,
            (extendedStyle & WindowStyles.WsExTransparent) != 0,
            (extendedStyle & WindowStyles.WsExNoactivate) != 0,
            (extendedStyle & WindowStyles.WsExToolwindow) != 0,
            (extendedStyle & WindowStyles.WsExTopmost) != 0);
}

public readonly record struct OverlayExtendedStyleApplyResult(
    bool Success,
    OverlayExtendedStyleState State,
    int Win32Error)
{
    public static OverlayExtendedStyleApplyResult Succeeded(long extendedStyle) =>
        new(true, OverlayExtendedStyleState.FromExtendedStyle(extendedStyle), 0);

    public static OverlayExtendedStyleApplyResult Failed(int win32Error, long extendedStyle) =>
        new(false, OverlayExtendedStyleState.FromExtendedStyle(extendedStyle), win32Error);
}

public static class OverlayExtendedStyleVerifier
{
    public static OverlayExtendedStyleApplyResult ApplyDrawingInputStyles(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return OverlayExtendedStyleApplyResult.Failed(0, 0);
        }

        var previous = WindowStyles.GetWindowLongPtr(windowHandle, WindowStyles.GwlExstyle);
        var desired = (long)previous;
        desired |= WindowStyles.WsExToolwindow | WindowStyles.WsExTopmost;
        desired &= ~WindowStyles.WsExTransparent;
        desired &= ~WindowStyles.WsExNoactivate;

        var setResult = WindowStyles.SetWindowLongPtr(windowHandle, WindowStyles.GwlExstyle, (nint)desired);
        if (setResult == 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                return OverlayExtendedStyleApplyResult.Failed(error, desired);
            }
        }

        _ = NativeMethods.SetWindowPos(
            windowHandle,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove |
            NativeMethods.SwpNosize |
            NativeMethods.SwpFramechanged |
            NativeMethods.SwpNoActivate);

        return ReadBack(windowHandle);
    }

    public static OverlayExtendedStyleApplyResult VerifyNoClickThrough(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return OverlayExtendedStyleApplyResult.Failed(0, 0);
        }

        return ReadBack(windowHandle);
    }

    public static bool IsClickThroughEnabled(long extendedStyle) =>
        (extendedStyle & WindowStyles.WsExTransparent) != 0;

    private static OverlayExtendedStyleApplyResult ReadBack(nint windowHandle)
    {
        var readBack = (long)WindowStyles.GetWindowLongPtr(windowHandle, WindowStyles.GwlExstyle);
        var state = OverlayExtendedStyleState.FromExtendedStyle(readBack);
        if (state.HasTransparent)
        {
            return OverlayExtendedStyleApplyResult.Failed(0, readBack);
        }

        return OverlayExtendedStyleApplyResult.Succeeded(readBack);
    }
}
