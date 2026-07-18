using System.Windows;
using System.Windows.Interop;
using Faktum.ScreenMarker.Platform.Windows;
using Faktum.ScreenMarker.Platform.Windows.Keyboard;

namespace Faktum.ScreenMarker.App.Hosting;

public sealed class HiddenMessageWindow : Window, IHiddenMessageWindowHost
{
    private readonly HotkeyRegistrationService? _hotkeys;
    private HwndSource? _hwndSource;
    private bool _handleReadyRaised;
    private bool _hookAttached;

    public HiddenMessageWindow()
        : this(null)
    {
    }

    internal HiddenMessageWindow(HotkeyRegistrationService? hotkeys)
    {
        _hotkeys = hotkeys;
        Width = 0;
        Height = 0;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        Visibility = Visibility.Hidden;
        Closed += (_, _) => DetachWndProcHook();
    }

    public nint Handle { get; private set; }

    public bool HasValidHandle => Handle != 0;

    internal bool IsWndProcHookAttached => _hookAttached;

    public event Action<int>? HotkeyPressed;

    public event Action? HandleReady;

    public event Action? InputLanguageChanged;

    public void EnsureHandleCreated()
    {
        if (Handle != 0)
        {
            RaiseHandleReadyIfNeeded();
            return;
        }

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        Handle = helper.Handle;
        AttachWndProcHookOnce();
        RaiseHandleReadyIfNeeded();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Handle = new WindowInteropHelper(this).Handle;
        AttachWndProcHookOnce();
        RaiseHandleReadyIfNeeded();
    }

    private void AttachWndProcHookOnce()
    {
        if (_hookAttached)
        {
            return;
        }

        var source = PresentationSource.FromVisual(this) as HwndSource
            ?? HwndSource.FromHwnd(Handle != 0 ? Handle : new WindowInteropHelper(this).Handle);
        if (source is null)
        {
            return;
        }

        source.AddHook(WndProc);
        _hwndSource = source;
        _hookAttached = true;
    }

    private void DetachWndProcHook()
    {
        if (!_hookAttached || _hwndSource is null)
        {
            return;
        }

        _hwndSource.RemoveHook(WndProc);
        _hookAttached = false;
        _hwndSource = null;
    }

    private void RaiseHandleReadyIfNeeded()
    {
        if (Handle == 0 || _handleReadyRaised)
        {
            return;
        }

        _handleReadyRaised = true;
        HandleReady?.Invoke();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WindowMessages.WmInputLanguageChange)
        {
            InputLanguageChanged?.Invoke();
            return 0;
        }

        if (_hotkeys is not null && HotkeyRegistrationService.ProcessWindowMessage((uint)msg, wParam, out var hotkeyId))
        {
            HotkeyPressed?.Invoke(hotkeyId);
            handled = true;
            return 0;
        }

        return 0;
    }
}
