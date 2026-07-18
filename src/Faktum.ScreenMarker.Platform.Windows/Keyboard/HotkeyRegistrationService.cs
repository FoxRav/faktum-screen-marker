using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;

namespace Faktum.ScreenMarker.Platform.Windows.Keyboard;

public readonly record struct PrimaryHotkeyRegistrationResult(
    bool AnyRegistered,
    bool AllRegistered,
    int RegisteredCount,
    int FailedCount,
    IReadOnlyList<uint> RegisteredVirtualKeys,
    IReadOnlyList<uint> FailedVirtualKeys);

public sealed class HotkeyRegistrationService : IDisposable
{
    public const int PrimaryHotkeyIdStart = 1;

    public const int FallbackHotkeyId = 9000;

    public const int MaxPrimaryHotkeySlots = 32;

    public const uint ModControl = 0x0002;

    public const uint ModShift = 0x0004;

    public const uint ModAlt = 0x0001;

    public const uint ModNorepeat = 0x4000;

    private readonly IHotkeyNativeApi _native;

    private readonly PhysicalActivationKeyResolver _keyResolver;

    private nint _windowHandle;

    private readonly List<uint> _registeredPrimaryVirtualKeys = [];

    private bool _fallbackRegistered;

    public HotkeyRegistrationService()
        : this(new Win32HotkeyNativeApi(), new PhysicalActivationKeyResolver(new Win32HotkeyNativeApi()))
    {
    }

    internal HotkeyRegistrationService(IHotkeyNativeApi native, PhysicalActivationKeyResolver keyResolver)
    {
        _native = native;
        _keyResolver = keyResolver;
    }

    public bool HasWindowHandle => _windowHandle != 0;

    public bool PrimaryRegistered => _registeredPrimaryVirtualKeys.Count > 0;

    public bool FallbackRegistered => _fallbackRegistered;

    public IReadOnlyList<uint> RegisteredPrimaryVirtualKeys => _registeredPrimaryVirtualKeys;

    public uint PrimaryVirtualKey => _registeredPrimaryVirtualKeys.Count > 0 ? _registeredPrimaryVirtualKeys[0] : 0;

    public static uint PrimaryModifiers => ModControl | ModNorepeat;

    public void AttachWindow(nint windowHandle)
    {
        if (_windowHandle == windowHandle)
        {
            return;
        }

        UnregisterAll();
        _windowHandle = windowHandle;
    }

    public PrimaryHotkeyRegistrationResult TryRegisterPrimary()
    {
        if (_windowHandle == 0)
        {
            return new PrimaryHotkeyRegistrationResult(false, false, 0, 0, [], []);
        }

        UnregisterPrimary();
        var uniqueVirtualKeys = _keyResolver.ResolveUniqueLayoutVirtualKeys();
        if (uniqueVirtualKeys.Count == 0)
        {
            DiagnosticLog.Write("PrimaryHotkey", "Could not resolve § key virtual key for any loaded layout.");
            return new PrimaryHotkeyRegistrationResult(false, false, 0, 0, [], []);
        }

        if (uniqueVirtualKeys.Count > MaxPrimaryHotkeySlots)
        {
            DiagnosticLog.Write("PrimaryHotkey", $"Too many unique layout mappings ({uniqueVirtualKeys.Count}); truncating to {MaxPrimaryHotkeySlots}.");
            uniqueVirtualKeys = uniqueVirtualKeys.Take(MaxPrimaryHotkeySlots).ToArray();
        }

        var registered = new List<uint>();
        var failed = new List<uint>();
        for (var index = 0; index < uniqueVirtualKeys.Count; index++)
        {
            var virtualKey = uniqueVirtualKeys[index];
            var hotkeyId = PrimaryHotkeyIdStart + index;
            if (_native.RegisterHotKey(_windowHandle, hotkeyId, PrimaryModifiers, virtualKey))
            {
                registered.Add(virtualKey);
            }
            else
            {
                failed.Add(virtualKey);
                DiagnosticLog.Write("PrimaryHotkey", $"RegisterHotKey failed for VK 0x{virtualKey:X} (id {hotkeyId}).");
            }
        }

        _registeredPrimaryVirtualKeys.Clear();
        _registeredPrimaryVirtualKeys.AddRange(registered);
        return new PrimaryHotkeyRegistrationResult(
            registered.Count > 0,
            failed.Count == 0,
            registered.Count,
            failed.Count,
            registered,
            failed);
    }

    public bool TryRegisterFallback() => TryRegisterFallback(ActivationHotkeys.Fallback);

    public bool TryRegisterFallback(ModifierHotkey hotkey)
    {
        if (_windowHandle == 0)
        {
            return false;
        }

        UnregisterFallback();
        var modifiers = BuildModifiers(hotkey);
        _fallbackRegistered = _native.RegisterHotKey(_windowHandle, FallbackHotkeyId, modifiers, (uint)hotkey.VirtualKey);
        if (!_fallbackRegistered)
        {
            DiagnosticLog.Write("FallbackHotkey", "RegisterHotKey failed.");
        }

        return _fallbackRegistered;
    }

    public PrimaryHotkeyRegistrationResult TryReregisterPrimaryAfterLayoutChange() => TryRegisterPrimary();

    public static bool ProcessWindowMessage(uint message, nint wParam, out int hotkeyId)
    {
        hotkeyId = 0;
        if (message != Native.NativeMethods.WmHotkey)
        {
            return false;
        }

        hotkeyId = (int)wParam;
        return IsKnownHotkeyId(hotkeyId);
    }

    public static bool IsKnownHotkeyId(int hotkeyId) =>
        hotkeyId is >= PrimaryHotkeyIdStart and < PrimaryHotkeyIdStart + MaxPrimaryHotkeySlots
        || hotkeyId == FallbackHotkeyId;

    public void UnregisterPrimary()
    {
        if (_windowHandle == 0)
        {
            _registeredPrimaryVirtualKeys.Clear();
            return;
        }

        for (var index = 0; index < MaxPrimaryHotkeySlots; index++)
        {
            _ = _native.UnregisterHotKey(_windowHandle, PrimaryHotkeyIdStart + index);
        }

        _registeredPrimaryVirtualKeys.Clear();
    }

    public void UnregisterFallback()
    {
        if (_fallbackRegistered && _windowHandle != 0)
        {
            _ = _native.UnregisterHotKey(_windowHandle, FallbackHotkeyId);
            _fallbackRegistered = false;
        }
    }

    public void UnregisterAll()
    {
        UnregisterPrimary();
        UnregisterFallback();
    }

    public void Dispose()
    {
        UnregisterAll();
        _windowHandle = 0;
    }

    private static uint BuildModifiers(ModifierHotkey hotkey)
    {
        uint modifiers = ModNorepeat;
        if (hotkey.Control)
        {
            modifiers |= ModControl;
        }

        if (hotkey.Shift)
        {
            modifiers |= ModShift;
        }

        if (hotkey.Alt)
        {
            modifiers |= ModAlt;
        }

        return modifiers;
    }
}
