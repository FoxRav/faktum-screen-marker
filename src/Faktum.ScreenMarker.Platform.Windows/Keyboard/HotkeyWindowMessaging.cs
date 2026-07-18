using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Keyboard;

public static class HotkeyWindowMessaging
{
    public static bool PostHotkeyMessage(nint windowHandle, int hotkeyId) =>
        NativeMethods.PostMessage(windowHandle, NativeMethods.WmHotkey, new nint(hotkeyId), 0);
}
