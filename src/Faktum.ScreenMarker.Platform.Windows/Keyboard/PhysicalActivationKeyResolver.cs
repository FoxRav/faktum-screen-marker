namespace Faktum.ScreenMarker.Platform.Windows.Keyboard;

/// <summary>
/// Resolves virtual keys for the physical key immediately left of top-row 1 (§ / ½ / `)
/// across all loaded keyboard layouts.
/// </summary>
public sealed class PhysicalActivationKeyResolver
{
    public const ushort SectionKeyScanCode = 0x29;

    public const uint MapVirtualKeyScanCodeToVirtualKey2 = 3;

    private readonly IHotkeyNativeApi _native;

    public PhysicalActivationKeyResolver(IHotkeyNativeApi native) => _native = native;

    public PhysicalActivationKeyResolver()
        : this(new Win32HotkeyNativeApi())
    {
    }

    public bool TryResolveVirtualKey(out uint virtualKey)
    {
        var keyboardLayout = _native.GetKeyboardLayout(0);
        virtualKey = _native.MapVirtualKey2(SectionKeyScanCode, MapVirtualKeyScanCodeToVirtualKey2, keyboardLayout);
        return virtualKey != 0;
    }

    public IReadOnlyList<uint> ResolveUniqueLayoutVirtualKeys()
    {
        var layouts = _native.GetKeyboardLayoutList();
        var uniqueKeys = new HashSet<uint>();
        foreach (var layout in layouts)
        {
            var virtualKey = _native.MapVirtualKey2(SectionKeyScanCode, MapVirtualKeyScanCodeToVirtualKey2, layout);
            if (virtualKey != 0)
            {
                uniqueKeys.Add(virtualKey);
            }
        }

        if (uniqueKeys.Count == 0 && TryResolveVirtualKey(out var activeLayoutKey))
        {
            uniqueKeys.Add(activeLayoutKey);
        }

        return uniqueKeys.OrderBy(static key => key).ToArray();
    }
}
