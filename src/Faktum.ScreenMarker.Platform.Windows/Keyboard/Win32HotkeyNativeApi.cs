using Faktum.ScreenMarker.Platform.Windows.Native;

namespace Faktum.ScreenMarker.Platform.Windows.Keyboard;

public sealed class Win32HotkeyNativeApi : IHotkeyNativeApi
{
    public nint GetKeyboardLayout(uint threadId) => NativeMethods.GetKeyboardLayout(threadId);

    public IReadOnlyList<nint> GetKeyboardLayoutList()
    {
        var required = NativeMethods.GetKeyboardLayoutList(0, null);
        if (required <= 0)
        {
            var active = GetKeyboardLayout(0);
            return active == 0 ? [] : [active];
        }

        var buffer = new nint[required];
        var count = NativeMethods.GetKeyboardLayoutList(required, buffer);
        if (count <= 0)
        {
            var active = GetKeyboardLayout(0);
            return active == 0 ? [] : [active];
        }

        return buffer.Take(count).ToArray();
    }

    public uint MapVirtualKey2(uint scanCode, uint mapType, nint keyboardLayout) =>
        NativeMethods.MapVirtualKeyEx(scanCode, mapType, keyboardLayout);

    public bool RegisterHotKey(nint windowHandle, int hotkeyId, uint modifiers, uint virtualKey) =>
        NativeMethods.RegisterHotKey(windowHandle, hotkeyId, modifiers, virtualKey);

    public bool UnregisterHotKey(nint windowHandle, int hotkeyId) =>
        NativeMethods.UnregisterHotKey(windowHandle, hotkeyId);
}
