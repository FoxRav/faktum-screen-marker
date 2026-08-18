using System.Windows;
using System.Windows.Forms;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.App.Tray;
using Faktum.ScreenMarker.Core.Application;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.Keyboard;
using Faktum.ScreenMarker.Platform.Windows.Settings;
using Faktum.ScreenMarker.Platform.Windows.Startup;
using Faktum.ScreenMarker.Platform.Windows.Windowing;

namespace Faktum.ScreenMarker.App.Hosting;

public sealed class ApplicationHost : IDisposable
{
    private readonly ApplicationStateCoordinator _stateCoordinator = new();
    private readonly DrawingLifecycleOrchestrator _lifecycle;
    private readonly ForegroundWindowService _foregroundWindowService = new();
    private readonly HotkeyRegistrationService _hotkeys = new();
    private readonly HiddenMessageWindow _messageWindow;
    private readonly HostInitializationCoordinator _initialization;
    private readonly TrayIconService _tray = new();
    private readonly OverlayCoordinator _overlayCoordinator = new();
    private readonly IStartupIntegration _startup;
    private readonly DisplayTopologyCoordinator _displayTopology;
    private SettingsPersistenceCoordinator? _settingsPersistence;

    private AppSettings _settings = AppSettings.CreateDefault();
    private DrawingSession? _session;
    private int _primaryHotkeyFailureNotifications;
    private int _fallbackHotkeyFailureNotifications;
    private int _drawingCommitFailureNotifications;
    private bool _stopped;
    private bool _disposed;

    public ApplicationHost() : this(new StartupIntegration())
    {
    }

    internal ApplicationHost(IStartupIntegration startupIntegration)
    {
        _startup = startupIntegration;
        _lifecycle = new DrawingLifecycleOrchestrator(
            _stateCoordinator,
            ActivateDrawing,
            () => DeactivateDrawing(force: false));

        _messageWindow = new HiddenMessageWindow(_hotkeys);
        _initialization = new HostInitializationCoordinator(_messageWindow, _hotkeys);
        _displayTopology = new DisplayTopologyCoordinator(
            _overlayCoordinator,
            new WpfUiDispatcher(),
            HandleDisplayTopologyChanged,
            HandleDisplayRebuildFailed);
        _overlayCoordinator.DrawingCommitFailed += NotifyDrawingCommitFailure;
    }

    internal ApplicationState State => _lifecycle.State;

    internal int PrimaryHotkeyFailureNotificationCount => _primaryHotkeyFailureNotifications;

    internal int FallbackHotkeyFailureNotificationCount => _fallbackHotkeyFailureNotifications;

    public void Start()
    {
        _settings = SettingsService.Load();
        AppApplication.ApplyCulture(_settings.LanguageOverride);
        _settingsPersistence = new SettingsPersistenceCoordinator(_settings, NotifySettingsSaveFailure);

        _initialization.HotkeyToggleRequested += RequestHotkeyToggle;
        _initialization.PrimaryHotkeyReregistrationFailed += NotifyPrimaryHotkeyFailure;
        _initialization.Initialize(() =>
        {
            _messageWindow.Show();
            _messageWindow.EnsureHandleCreated();
        });

        _tray.Initialize(BuildTrayMenu());
        _tray.ShowBalloon(AppApplication.GetString("AppStartedTitle"), AppApplication.GetString("AppStartedMessage"));

        if (_messageWindow.HasValidHandle)
        {
            _hotkeys.AttachWindow(_messageWindow.Handle);
            if (!_initialization.TryRegisterPrimaryHotkey())
            {
                NotifyPrimaryHotkeyFailure();
            }

            if (!_initialization.TryRegisterFallbackHotkey())
            {
                NotifyFallbackHotkeyFailure();
            }
        }

        SyncStartupRegistry(_settings.StartWithWindows);

        _lifecycle.MarkStarted();
    }

    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _lifecycle.MarkStopping();
        DeactivateDrawing(force: true);
        _displayTopology.Dispose();
        _settingsPersistence?.Flush();
        _settingsPersistence?.Dispose();
        _initialization.PrimaryHotkeyReregistrationFailed -= NotifyPrimaryHotkeyFailure;
        _initialization.Dispose();
        _hotkeys.Dispose();
        _messageWindow.Close();
        _tray.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    internal void RequestHotkeyToggle() =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(_lifecycle.RequestHotkeyToggle);

    internal void RequestExplicitActivate() =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(_lifecycle.RequestExplicitActivate);

    internal void RequestExplicitDeactivate() =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(_lifecycle.RequestExplicitDeactivate);

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(AppApplication.GetString("TrayActivate"), null, (_, _) => RequestExplicitActivate()));
        menu.Items.Add(new ToolStripMenuItem(AppApplication.GetString("TrayDeactivate"), null, (_, _) => RequestExplicitDeactivate()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(AppApplication.GetString("TraySettings"), null, (_, _) => OpenSettings()));
        menu.Items.Add(new ToolStripMenuItem(AppApplication.GetString("TrayAbout"), null, (_, _) =>
            System.Windows.MessageBox.Show(AppApplication.GetString("AboutMessage"), AppApplication.GetString("AboutTitle"), MessageBoxButton.OK, MessageBoxImage.Information)));

        var startupItem = new ToolStripMenuItem(AppApplication.GetString("TrayStartup"))
        {
            Checked = _settings.StartWithWindows,
            CheckOnClick = true,
        };
        startupItem.CheckedChanged += (_, _) => ApplyStartWithWindowsChange(startupItem.Checked);

        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(AppApplication.GetString("TrayExit"), null, (_, _) =>
        {
            DeactivateDrawing(force: true);
            System.Windows.Application.Current.Shutdown();
        }));
        return menu;
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settingsPersistence!.Current);
        if (window.ShowDialog() == true && window.ResultSettings is AppSettings updated)
        {
            _settingsPersistence.ApplySettings(updated);
            _settings = _settingsPersistence.Current;
            AppApplication.ApplyCulture(_settings.LanguageOverride);
            ApplyStartWithWindowsChange(_settings.StartWithWindows);
        }
    }

    private void SyncStartupRegistry(bool enabled)
    {
        try
        {
            _startup.Apply(enabled, Environment.ProcessPath ?? string.Empty);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Startup", ex.GetType().Name);
            _tray.ShowBalloon(AppApplication.GetString("StartupFailedTitle"), AppApplication.GetString("StartupFailedMessage"));
        }
    }

    internal void ApplyStartWithWindowsChange(bool enabled)
    {
        _settingsPersistence?.UpdateStartWithWindows(enabled);
        _settingsPersistence?.Flush();
        SyncStartupRegistry(enabled);
    }

    private void ActivateDrawing()
    {
        try
        {
            _foregroundWindowService.Capture();
            _session?.Dispose();
            _session = new DrawingSession
            {
                ActiveStyle = new StrokeStyle(_settings.PreferredColor, _settings.PreferredStrokeWidth),
            };

            var success = _overlayCoordinator.Activate(_session, _settings, _settingsPersistence!, DeactivateFromUi);
            if (!success)
            {
                _lifecycle.NotifyActivationFailed(AppApplication.GetString("ActivationFailed"));
                DiagnosticLog.Write("Overlay", "Overlay activation failed.");
                _tray.ShowBalloon(AppApplication.GetString("ActivationFailedTitle"), AppApplication.GetString("ActivationFailed"));
                _session.Dispose();
                _session = null;
                _foregroundWindowService.RestoreBestEffort();
                return;
            }

            _displayTopology.SetActive(true, _session, _settings, DeactivateFromUi, NotifyDisplayChanged);
            _displayTopology.MarkInitialized();
            _lifecycle.NotifyActivationSucceeded();
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Activation", ex.GetType().Name);
            _lifecycle.NotifyFaultRecoverable(ex.GetType().Name);
            _tray.ShowBalloon(AppApplication.GetString("ActivationFailedTitle"), AppApplication.GetString("ActivationFailed"));
            DeactivateDrawing(force: true);
        }
    }

    private void DeactivateDrawing(bool force)
    {
        if (!force && _lifecycle.State != ApplicationState.Deactivating && _lifecycle.State != ApplicationState.Active)
        {
            return;
        }

        _displayTopology.SetActive(false, null, null, null, null);
        _overlayCoordinator.Deactivate();
        _session?.Dispose();
        _session = null;
        _foregroundWindowService.RestoreBestEffort();
        _settingsPersistence?.Flush();
        _lifecycle.NotifyDeactivationComplete();
    }

    private void DeactivateFromUi() => RequestExplicitDeactivate();

    private void HandleDisplayTopologyChanged()
    {
        _tray.ShowBalloon(AppApplication.GetString("DisplayChangedTitle"), AppApplication.GetString("DisplayChangedMessage"));
    }

    private void HandleDisplayRebuildFailed()
    {
        _lifecycle.NotifyDisplayRebuildFailed();
        _displayTopology.SetActive(false, null, null, null, null);
        _overlayCoordinator.Deactivate();
        _session?.Dispose();
        _session = null;
        _foregroundWindowService.RestoreBestEffort();
        _settingsPersistence?.Flush();
    }

    private void NotifyDisplayChanged(string titleKey, string messageKey) =>
        _tray.ShowBalloon(AppApplication.GetString(titleKey), AppApplication.GetString(messageKey));

    private void NotifySettingsSaveFailure(string titleKey, string messageKey) =>
        _tray.ShowBalloon(AppApplication.GetString(titleKey), AppApplication.GetString(messageKey));

    private void NotifyPrimaryHotkeyFailure()
    {
        _primaryHotkeyFailureNotifications++;
        _tray.ShowBalloon(
            AppApplication.GetString("PrimaryHotkeyFailedTitle"),
            AppApplication.GetString("PrimaryHotkeyFailedMessage"));
    }

    private void NotifyFallbackHotkeyFailure()
    {
        _fallbackHotkeyFailureNotifications++;
        _tray.ShowBalloon(
            AppApplication.GetString("FallbackHotkeyFailedTitle"),
            AppApplication.GetString("FallbackHotkeyFailedMessage"));
    }

    private void NotifyDrawingCommitFailure()
    {
        _drawingCommitFailureNotifications++;
        _tray.ShowBalloon(
            AppApplication.GetString("DrawingCommitFailedTitle"),
            AppApplication.GetString("DrawingCommitFailedMessage"));
    }
}
