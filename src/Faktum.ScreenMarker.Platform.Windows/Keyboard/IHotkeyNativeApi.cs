namespace Faktum.ScreenMarker.Platform.Windows.Keyboard;

public interface IHotkeyNativeApi
{
    nint GetKeyboardLayout(uint threadId);

    IReadOnlyList<nint> GetKeyboardLayoutList();

    uint MapVirtualKey2(uint scanCode, uint mapType, nint keyboardLayout);

    bool RegisterHotKey(nint windowHandle, int hotkeyId, uint modifiers, uint virtualKey);

    bool UnregisterHotKey(nint windowHandle, int hotkeyId);
}
