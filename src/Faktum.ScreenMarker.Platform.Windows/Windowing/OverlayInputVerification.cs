using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Windowing;

public static class OverlayInputVerification
{
    public static bool WindowFromPointMatches(nint overlayWindowHandle, int screenX, int screenY)
    {
        if (overlayWindowHandle == 0)
        {
            return false;
        }

        var hwnd = NativeMethods.WindowFromPoint(new NativeMethods.NativePoint { X = screenX, Y = screenY });
        return hwnd == overlayWindowHandle;
    }
}
