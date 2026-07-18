using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.Keyboard;

namespace Faktum.ScreenMarker.App.Hosting;

/// <summary>
/// Coordinates hidden-window and hotkey initialization with testable call ordering.
/// Owns input-layout change registration and primary hotkey re-registration.
/// </summary>
public sealed class HostInitializationCoordinator : IDisposable
{
    private readonly IHiddenMessageWindowHost _messageWindow;
    private readonly HotkeyRegistrationService _hotkeys;
    private readonly List<string> _callLog = [];
    private bool _subscribed;
    private bool _handleReadyHandled;
    private bool _disposed;

    public HostInitializationCoordinator(IHiddenMessageWindowHost messageWindow, HotkeyRegistrationService hotkeys)
    {
        _messageWindow = messageWindow;
        _hotkeys = hotkeys;
    }

    public IReadOnlyList<string> CallLog => _callLog;

    public bool PrimaryRegistered { get; private set; }

    public bool FallbackRegistered { get; private set; }

    public event Action? HotkeyToggleRequested;

    public event Action? PrimaryHotkeyReregistrationFailed;

    public void Initialize(Action ensureHiddenWindowHandle)
    {
        if (_subscribed)
        {
            return;
        }

        Log("SubscribeEvents");
        _messageWindow.HandleReady += OnHandleReady;
        _messageWindow.HotkeyPressed += OnHotkeyPressed;
        _messageWindow.InputLanguageChanged += OnInputLanguageChanged;
        _subscribed = true;

        Log("EnsureHiddenWindowHandle");
        ensureHiddenWindowHandle();
    }

    public bool TryRegisterPrimaryHotkey()
    {
        if (!_messageWindow.HasValidHandle)
        {
            PrimaryRegistered = false;
            return false;
        }

        Log("RegisterPrimaryHotkey");
        var result = _hotkeys.TryRegisterPrimary();
        PrimaryRegistered = result.AnyRegistered;
        if (!PrimaryRegistered)
        {
            DiagnosticLog.Write("PrimaryHotkey", "RegisterHotKey failed for all layout mappings.");
        }
        else if (!result.AllRegistered)
        {
            DiagnosticLog.Write("PrimaryHotkey", $"Partial primary registration: {result.RegisteredCount} succeeded, {result.FailedCount} failed.");
        }

        return PrimaryRegistered;
    }

    public bool TryRegisterFallbackHotkey()
    {
        if (!_messageWindow.HasValidHandle)
        {
            FallbackRegistered = false;
            return false;
        }

        Log("RegisterFallbackHotkey");
        FallbackRegistered = _hotkeys.TryRegisterFallback();
        if (!FallbackRegistered)
        {
            DiagnosticLog.Write("FallbackHotkey", "RegisterHotKey failed.");
        }

        return FallbackRegistered;
    }

    public bool TryReregisterPrimaryAfterLayoutChange()
    {
        if (!_messageWindow.HasValidHandle)
        {
            PrimaryRegistered = false;
            return false;
        }

        Log("ReregisterPrimaryHotkey");
        var result = _hotkeys.TryReregisterPrimaryAfterLayoutChange();
        PrimaryRegistered = result.AnyRegistered;
        if (!PrimaryRegistered)
        {
            DiagnosticLog.Write("PrimaryHotkey", "RegisterHotKey failed after layout change.");
            PrimaryHotkeyReregistrationFailed?.Invoke();
        }
        else if (!result.AllRegistered)
        {
            DiagnosticLog.Write("PrimaryHotkey", $"Partial primary re-registration: {result.RegisteredCount} succeeded, {result.FailedCount} failed.");
            PrimaryHotkeyReregistrationFailed?.Invoke();
        }

        return PrimaryRegistered;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Log("UnsubscribeEvents");
        _messageWindow.HandleReady -= OnHandleReady;
        _messageWindow.HotkeyPressed -= OnHotkeyPressed;
        _messageWindow.InputLanguageChanged -= OnInputLanguageChanged;
        Log("UnregisterHotkeys");
        _hotkeys.UnregisterAll();
    }

    private void OnHandleReady()
    {
        if (_handleReadyHandled)
        {
            return;
        }

        _handleReadyHandled = true;
        Log("HandleReady");
        _hotkeys.AttachWindow(_messageWindow.Handle);
    }

    private void OnHotkeyPressed(int hotkeyId)
    {
        if (HotkeyRegistrationService.IsKnownHotkeyId(hotkeyId))
        {
            HotkeyToggleRequested?.Invoke();
        }
    }

    private void OnInputLanguageChanged()
    {
        _ = TryReregisterPrimaryAfterLayoutChange();
    }

    private void Log(string step) => _callLog.Add(step);
}
