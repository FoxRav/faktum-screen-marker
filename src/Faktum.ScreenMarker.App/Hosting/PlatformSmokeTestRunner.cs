using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Faktum.ScreenMarker.App.Hosting;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.App.Toolbar;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Keyboard;
using Faktum.ScreenMarker.Platform.Windows.Monitors;
using Faktum.ScreenMarker.Platform.Windows.Native;
using Faktum.ScreenMarker.Platform.Windows.Windowing;

namespace Faktum.ScreenMarker.App;

internal static class PlatformSmokeTestRunner
{
    public static int Run()
    {
        var dispatcherFrame = new DispatcherFrame();
        var exitCode = 1;

        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            try
            {
                exitCode = RunOnUiThread();
            }
            catch
            {
                exitCode = 2;
            }
            finally
            {
                dispatcherFrame.Continue = false;
            }
        });

        Dispatcher.PushFrame(dispatcherFrame);
        return exitCode;
    }

    private static int RunOnUiThread()
    {
        var hotkeyExit = RunHotkeySmoke();
        if (hotkeyExit != 0)
        {
            return hotkeyExit;
        }

        return RunOverlayToolbarSmoke();
    }

    private static int RunHotkeySmoke()
    {
        using var hotkeys = new HotkeyRegistrationService();
        var messageWindow = new HiddenMessageWindow(hotkeys);
        messageWindow.EnsureHandleCreated();
        if (!messageWindow.HasValidHandle)
        {
            return 3;
        }

        if (!messageWindow.IsWndProcHookAttached)
        {
            return 4;
        }

        messageWindow.EnsureHandleCreated();
        if (!messageWindow.IsWndProcHookAttached)
        {
            return 5;
        }

        hotkeys.AttachWindow(messageWindow.Handle);
        var primaryResult = hotkeys.TryRegisterPrimary();
        if (!primaryResult.AnyRegistered)
        {
            return 6;
        }

        if (primaryResult.RegisteredVirtualKeys.Count == 0 || primaryResult.RegisteredVirtualKeys.Any(static key => key == 0))
        {
            return 7;
        }

        if ((HotkeyRegistrationService.PrimaryModifiers & HotkeyRegistrationService.ModNorepeat) == 0)
        {
            return 8;
        }

        if (!hotkeys.TryRegisterFallback())
        {
            return 9;
        }

        var toggleEvents = 0;
        messageWindow.HotkeyPressed += _ => toggleEvents++;

        for (var index = 0; index < primaryResult.RegisteredVirtualKeys.Count; index++)
        {
            var hotkeyId = HotkeyRegistrationService.PrimaryHotkeyIdStart + index;
            if (!PostHotkey(messageWindow.Handle, hotkeyId))
            {
                return 10 + index;
            }

            PumpMessages();
        }

        if (toggleEvents != primaryResult.RegisteredVirtualKeys.Count)
        {
            return 20;
        }

        if (!PostHotkey(messageWindow.Handle, HotkeyRegistrationService.FallbackHotkeyId))
        {
            return 21;
        }

        PumpMessages();
        if (toggleEvents != primaryResult.RegisteredVirtualKeys.Count + 1)
        {
            return 22;
        }

        if (!PostHotkey(messageWindow.Handle, 99))
        {
            return 23;
        }

        PumpMessages();
        if (toggleEvents != primaryResult.RegisteredVirtualKeys.Count + 1)
        {
            return 24;
        }

        hotkeys.UnregisterAll();
        if (hotkeys.PrimaryRegistered || hotkeys.FallbackRegistered)
        {
            return 25;
        }

        messageWindow.Close();
        PumpMessages();
        return 0;
    }

    private static int RunOverlayToolbarSmoke()
    {
        using var session = new DrawingSession();
        var settings = AppSettings.CreateDefault();
        var coordinator = new OverlayCoordinator();
        using var persistence = new SettingsPersistenceCoordinator(settings);

        if (!coordinator.Activate(session, settings, persistence, () => coordinator.Deactivate()))
        {
            coordinator.Deactivate();
            PumpMessages();
            return 30;
        }

        var overlay = coordinator.Overlays.Count > 0 ? coordinator.Overlays[0] : null;
        var toolbar = coordinator.Toolbar;
        if (overlay is null || toolbar is null)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 31;
        }

        var overlayHandle = new WindowInteropHelper(overlay).Handle;
        if (overlayHandle == 0)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 32;
        }

        var styleState = OverlayExtendedStyleVerifier.VerifyNoClickThrough(overlayHandle);
        if (!styleState.Success || styleState.State.HasTransparent)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 33;
        }

        if (overlay.InputSurface.ActualWidth <= 0 || overlay.InputSurface.ActualHeight <= 0)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 34;
        }

        foreach (var automationId in OverlayVisualHitTesting.AllToolbarControlAutomationIds)
        {
            var control = toolbar.FindControlByAutomationId(automationId);
            if (control is null)
            {
                coordinator.Deactivate();
                PumpMessages();
                return 100 + Array.IndexOf(OverlayVisualHitTesting.AllToolbarControlAutomationIds.ToArray(), automationId);
            }

            if (!OverlayVisualHitTesting.HitTestControlCenter(overlay.OverlayRoot, control, out _))
            {
                coordinator.Deactivate();
                PumpMessages();
                return 100 + Array.IndexOf(OverlayVisualHitTesting.AllToolbarControlAutomationIds.ToArray(), automationId);
            }
        }

        if (!coordinator.VerifyToolbarHitTesting())
        {
            coordinator.Deactivate();
            PumpMessages();
            return 35;
        }

        session.BeginPreview(overlay.Monitor.DeviceName, new FreehandStroke(
            session.AllocateId(),
            overlay.Monitor.DeviceName,
            session.ActiveStyle,
            [new Point2D(20, 20), new Point2D(40, 40)]));
        if (!session.CommitPreview())
        {
            coordinator.Deactivate();
            PumpMessages();
            return 36;
        }

        if (!coordinator.VerifyToolbarHitTesting())
        {
            coordinator.Deactivate();
            PumpMessages();
            return 37;
        }

        var lineControl = toolbar.FindControlByAutomationId(ToolbarControlIds.Tool.Line);
        if (lineControl is not FrameworkElement lineButton)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 38;
        }

        ToolbarRoutedInput.InvokeAtCenter(lineButton, overlay.OverlayRoot);
        WpfTestContextPump();
        if (session.ActiveTool != DrawingTool.Line || toolbar.SelectedToolButtonCount != 1)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 38;
        }

        var fontSizeControl = toolbar.FindControlByAutomationId(ToolbarControlIds.Text.FontSize);
        if (fontSizeControl is null)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 40;
        }

        overlay.RaiseEvent(new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice,
            System.Windows.PresentationSource.FromVisual(overlay),
            0,
            System.Windows.Input.Key.Q)
        {
            RoutedEvent = System.Windows.UIElement.PreviewKeyDownEvent,
        });
        WpfTestContextPump();
        if (session.ActiveTool != DrawingTool.Pen)
        {
            coordinator.Deactivate();
            PumpMessages();
            return 41;
        }

        coordinator.Deactivate();
        PumpMessages();

        if (System.Windows.Application.Current.Windows.OfType<OverlayWindow>().Any())
        {
            return 39;
        }

        return 0;
    }

    private static bool PostHotkey(nint handle, int hotkeyId) =>
        HotkeyWindowMessaging.PostHotkeyMessage(handle, hotkeyId);

    private static void PumpMessages()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void WpfTestContextPump() => PumpMessages();
}
