using System.Windows;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.App.Toolbar;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.Monitors;

namespace Faktum.ScreenMarker.App.Overlays;

public interface IOverlayWindowFactory
{
    OverlayWindow Create(MonitorInfo monitor, DrawingSession session);
}

public sealed class DefaultOverlayWindowFactory : IOverlayWindowFactory
{
    public OverlayWindow Create(MonitorInfo monitor, DrawingSession session) => new(monitor, session);
}

public sealed class OverlayCoordinator : IDisposable
{
    private readonly IOverlayWindowFactory _overlayFactory;
    private readonly List<string> _rebuildCallLog = [];
    private readonly List<OverlayWindow> _overlays = [];
    private OverlayWindow? _pointerOverlay;
    private ToolbarControl? _toolbar;
    private ToolbarInteractionCoordinator? _interactionCoordinator;
    private DrawingSession? _session;
    private double? _preservedToolbarLeft;
    private double? _preservedToolbarTop;

    public OverlayCoordinator()
        : this(new DefaultOverlayWindowFactory())
    {
    }

    internal OverlayCoordinator(IOverlayWindowFactory overlayFactory)
    {
        _overlayFactory = overlayFactory;
    }

    public IReadOnlyList<string> RebuildCallLog => _rebuildCallLog;

    public IReadOnlyList<OverlayWindow> Overlays => _overlays;

    internal ToolbarControl? Toolbar => _toolbar;

    internal ToolbarInteractionCoordinator? InteractionCoordinator => _interactionCoordinator;

    public event Action? DrawingCommitFailed;

    public bool Activate(
        DrawingSession session,
        AppSettings settings,
        SettingsPersistenceCoordinator settingsPersistence,
        Action deactivateRequested,
        ITextEditorHost? textEditorHost = null)
    {
        Deactivate();
        _session = session;

        var monitors = MonitorEnumerator.EnumerateActiveMonitors();
        if (monitors.Count == 0)
        {
            return false;
        }

        try
        {
            _interactionCoordinator = new ToolbarInteractionCoordinator(
                session,
                this,
                settingsPersistence,
                deactivateRequested,
                textEditorHost);
            _interactionCoordinator.DrawingCommitFailed += OnDrawingCommitFailed;

            foreach (var monitor in monitors)
            {
                var overlay = _overlayFactory.Create(monitor, session);
                overlay.BindInteractionCoordinator(_interactionCoordinator);
                overlay.DrawingCommitFailed += OnDrawingCommitFailed;
                overlay.Show();
                if (!overlay.InitializeInput())
                {
                    DiagnosticLog.Write("OverlayInput", overlay.InputDiagnosticState.ToPrivacySafeLine());
                    Deactivate();
                    return false;
                }

                _overlays.Add(overlay);
            }

            _pointerOverlay = FindMonitorOverlay(monitors);
            _toolbar = new ToolbarControl(session, settings, settingsPersistence, _interactionCoordinator, _pointerOverlay.Monitor);
            _pointerOverlay.ToolbarHost.Attach(_toolbar, settings, settingsPersistence, _pointerOverlay.Monitor);
            EnsureLayoutReady();

            if (!ValidateActivation(settingsPersistence))
            {
                Deactivate();
                return false;
            }

            return true;
        }
        catch
        {
            Deactivate();
            return false;
        }
    }

    public void RebuildOverlays(
        DrawingSession session,
        AppSettings settings,
        Action deactivateRequested)
    {
        _rebuildCallLog.Clear();
        _session = session;

        var monitors = MonitorEnumerator.EnumerateActiveMonitors();
        if (monitors.Count == 0)
        {
            throw new InvalidOperationException("No active monitors available for overlay rebuild.");
        }

        var existingToolbar = _toolbar;
        var existingCoordinator = _interactionCoordinator;
        var settingsPersistence = existingToolbar?.SettingsPersistence;

        existingCoordinator?.OnDisplayRebuild();

        if (existingToolbar is not null && _pointerOverlay is not null)
        {
            LogRebuildStep("PreserveToolbarState");
            if (_pointerOverlay.ToolbarHost.TryGetLocalPlacement(out var left, out var top))
            {
                _preservedToolbarLeft = left;
                _preservedToolbarTop = top;
            }

            LogRebuildStep("FlushToolbarSettingsOnce");
            existingToolbar.FlushSettings();
            LogRebuildStep("DetachToolbarFromOverlay");
            _pointerOverlay.ToolbarHost.Detach();
        }

        LogRebuildStep("CloseOldOverlays");
        foreach (var overlay in _overlays)
        {
            overlay.DrawingCommitFailed -= OnDrawingCommitFailed;
            overlay.ReleaseInputCapture();
            overlay.Close();
        }

        _overlays.Clear();
        _pointerOverlay = null;

        if (existingCoordinator is not null)
        {
            existingCoordinator.DrawingCommitFailed -= OnDrawingCommitFailed;
            existingCoordinator.Dispose();
            _interactionCoordinator = null;
        }

        if (settingsPersistence is not null)
        {
            _interactionCoordinator = new ToolbarInteractionCoordinator(session, this, settingsPersistence, deactivateRequested);
            _interactionCoordinator.DrawingCommitFailed += OnDrawingCommitFailed;
        }

        LogRebuildStep("CreateNewOverlays");
        foreach (var monitor in monitors)
        {
            var overlay = _overlayFactory.Create(monitor, session);
            if (_interactionCoordinator is not null)
            {
                overlay.BindInteractionCoordinator(_interactionCoordinator);
            }

            overlay.DrawingCommitFailed += OnDrawingCommitFailed;
            overlay.Show();
            if (!overlay.InitializeInput())
            {
                DiagnosticLog.Write("OverlayInput", overlay.InputDiagnosticState.ToPrivacySafeLine());
                throw new InvalidOperationException("Overlay input initialization failed during display rebuild.");
            }

            _overlays.Add(overlay);
        }

        LogRebuildStep("SelectPointerOverlay");
        _pointerOverlay = FindMonitorOverlay(monitors);

        if (existingToolbar is not null && _interactionCoordinator is not null)
        {
            LogRebuildStep("ReattachExistingToolbar");
            existingToolbar.UpdateMonitor(_pointerOverlay.Monitor);
            _interactionCoordinator.AttachToolbar(existingToolbar);
            _pointerOverlay.ToolbarHost.Attach(existingToolbar, settings, settingsPersistence!, _pointerOverlay.Monitor);
            if (_preservedToolbarLeft is double left && _preservedToolbarTop is double top)
            {
                _pointerOverlay.ToolbarHost.RestoreLocalPlacement(left, top);
            }

            _toolbar = existingToolbar;
        }
        else if (settingsPersistence is not null && _interactionCoordinator is not null)
        {
            LogRebuildStep("RecreateToolbar");
            _toolbar = new ToolbarControl(session, settings, settingsPersistence, _interactionCoordinator, _pointerOverlay.Monitor);
            _pointerOverlay.ToolbarHost.Attach(_toolbar, settings, settingsPersistence, _pointerOverlay.Monitor);
            if (_preservedToolbarLeft is double left && _preservedToolbarTop is double top)
            {
                _pointerOverlay.ToolbarHost.RestoreLocalPlacement(left, top);
            }
        }

        LogRebuildStep("VerifyToolbarHitTesting");
        EnsureLayoutReady();
        if (!VerifyToolbarHitTesting())
        {
            throw new InvalidOperationException("Toolbar hit testing failed during display rebuild.");
        }
    }

    public void Deactivate()
    {
        _interactionCoordinator?.BeginDeactivation();

        foreach (var overlay in _overlays)
        {
            overlay.DrawingCommitFailed -= OnDrawingCommitFailed;
        }

        if (_interactionCoordinator is not null)
        {
            _interactionCoordinator.DrawingCommitFailed -= OnDrawingCommitFailed;
            _interactionCoordinator.Dispose();
            _interactionCoordinator = null;
        }

        _toolbar?.FlushSettings();
        if (_pointerOverlay is not null)
        {
            _pointerOverlay.ToolbarHost.Detach();
        }

        _toolbar?.Dispose();
        _toolbar = null;

        foreach (var overlay in _overlays)
        {
            overlay.ReleaseInputCapture();
            overlay.Close();
        }

        _overlays.Clear();
        _pointerOverlay = null;
        _session = null;
        _preservedToolbarLeft = null;
        _preservedToolbarTop = null;
        _rebuildCallLog.Clear();
    }

    public void Dispose() => Deactivate();

    internal bool VerifyToolbarHitTesting()
    {
        if (_toolbar is null || _pointerOverlay is null)
        {
            return false;
        }

        EnsureLayoutReady();
        if (!OverlayVisualHitTesting.VerifyToolbarAndInputSurface(_pointerOverlay, _toolbar, out var failedAutomationId))
        {
            DiagnosticLog.Write("ToolbarHitTest", failedAutomationId ?? "unknown");
            return false;
        }

        return true;
    }

    private bool ValidateActivation(SettingsPersistenceCoordinator settingsPersistence)
    {
        if (_toolbar is null || _pointerOverlay is null || _interactionCoordinator is null)
        {
            DiagnosticLog.Write("Activation", "Missing toolbar or pointer overlay.");
            return false;
        }

        if (_toolbar.RegisteredToolButtonCount != 7)
        {
            DiagnosticLog.Write("Activation", "Tool button count invalid.");
            return false;
        }

        foreach (var automationId in OverlayVisualHitTesting.AllToolbarControlAutomationIds)
        {
            if (_toolbar.FindControlByAutomationId(automationId) is null)
            {
                DiagnosticLog.Write("Activation", automationId);
                return false;
            }
        }

        if (_session?.ActiveTool != DrawingTool.Pen || !_toolbar.IsToolButtonSelected(DrawingTool.Pen))
        {
            DiagnosticLog.Write("Activation", "Pen not selected.");
            return false;
        }

        if (!VerifyToolbarHitTesting())
        {
            return false;
        }

        _ = settingsPersistence;
        return true;
    }

    private void EnsureLayoutReady()
    {
        foreach (var overlay in _overlays)
        {
            overlay.UpdateLayout();
            var width = overlay.Width > 0 ? overlay.Width : overlay.Monitor.Width / overlay.Monitor.DipScaleX;
            var height = overlay.Height > 0 ? overlay.Height : overlay.Monitor.Height / overlay.Monitor.DipScaleY;
            overlay.OverlayRoot.Measure(new System.Windows.Size(width, height));
            overlay.OverlayRoot.Arrange(new Rect(0, 0, width, height));
            overlay.ToolbarHost.Measure(new System.Windows.Size(width, height));
            overlay.ToolbarHost.Arrange(new Rect(0, 0, width, height));
        }

        _toolbar?.EnsureMeasured();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            static () => { },
            System.Windows.Threading.DispatcherPriority.Render);
    }

    private void OnDrawingCommitFailed() => DrawingCommitFailed?.Invoke();

    private void LogRebuildStep(string step) => _rebuildCallLog.Add(step);

    private OverlayWindow FindMonitorOverlay(IReadOnlyList<MonitorInfo> monitors)
    {
        var pointerMonitor = FindMonitorForPointer(monitors);
        foreach (var overlay in _overlays)
        {
            if (overlay.Monitor.DeviceName == pointerMonitor.DeviceName)
            {
                return overlay;
            }
        }

        return _overlays[0];
    }

    private static MonitorInfo FindMonitorForPointer(IReadOnlyList<MonitorInfo> monitors)
    {
        var position = FormsCursorPosition();
        foreach (var monitor in monitors)
        {
            if (position.X >= monitor.Left && position.X < monitor.Left + monitor.Width &&
                position.Y >= monitor.Top && position.Y < monitor.Top + monitor.Height)
            {
                return monitor;
            }
        }

        return monitors[0];
    }

    private static (double X, double Y) FormsCursorPosition()
    {
        var p = System.Windows.Forms.Cursor.Position;
        return (p.X, p.Y);
    }
}
