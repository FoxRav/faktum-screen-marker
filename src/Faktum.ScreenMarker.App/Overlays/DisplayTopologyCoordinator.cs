using Faktum.ScreenMarker.App.Hosting;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.Monitors;
using Microsoft.Win32;

namespace Faktum.ScreenMarker.App.Overlays;

public sealed class DisplayTopologyCoordinator : IDisposable
{
    private readonly OverlayCoordinator _overlayCoordinator;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IDebounceScheduler _debounceScheduler;
    private readonly Action _onDisplayTopologyChanged;
    private readonly Action? _onRebuildFailed;
    private readonly object _gate = new();
    private bool _active;
    private bool _initialized;
    private bool _rebuildInProgress;
    private bool _pendingRebuild;
    private bool _disposed;
    private long _activationGeneration;
    private long _rebuildGeneration;
    private DrawingSession? _session;
    private AppSettings? _settings;
    private Action? _deactivateRequested;
    private Action<string, string>? _notifyFailure;

    public DisplayTopologyCoordinator(
        OverlayCoordinator overlayCoordinator,
        IUiDispatcher uiDispatcher,
        Action onDisplayTopologyChanged,
        Action? onRebuildFailed = null)
        : this(overlayCoordinator, uiDispatcher, new TimerDebounceScheduler(), onDisplayTopologyChanged, onRebuildFailed)
    {
    }

    internal DisplayTopologyCoordinator(
        OverlayCoordinator overlayCoordinator,
        IUiDispatcher uiDispatcher,
        IDebounceScheduler debounceScheduler,
        Action onDisplayTopologyChanged,
        Action? onRebuildFailed = null)
    {
        _overlayCoordinator = overlayCoordinator;
        _uiDispatcher = uiDispatcher;
        _debounceScheduler = debounceScheduler;
        _onDisplayTopologyChanged = onDisplayTopologyChanged;
        _onRebuildFailed = onRebuildFailed;
        SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;
    }

    public void SetActive(
        bool active,
        DrawingSession? session,
        AppSettings? settings,
        Action? deactivateRequested,
        Action<string, string>? notifyFailure)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _active = active;
            _session = session;
            _settings = settings;
            _deactivateRequested = deactivateRequested;
            _notifyFailure = notifyFailure;
            if (active)
            {
                _activationGeneration++;
                _rebuildGeneration = _activationGeneration;
            }
            else
            {
                _initialized = false;
                _pendingRebuild = false;
                _rebuildInProgress = false;
                _rebuildGeneration = 0;
            }
        }

        if (!active)
        {
            _debounceScheduler.CancelPending();
        }
    }

    public void MarkInitialized()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_active)
            {
                _initialized = true;
            }
        }
    }

    internal void TestScheduleRebuild() => ScheduleRebuildIfNeeded();

    internal void TestRaiseDisplaySettingsChanged() => OnSystemDisplaySettingsChanged(null, EventArgs.Empty);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _active = false;
            _initialized = false;
            _pendingRebuild = false;
            _rebuildInProgress = false;
            _session = null;
            _settings = null;
            _deactivateRequested = null;
            _notifyFailure = null;
        }

        _debounceScheduler.CancelPending();
        SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;
        _debounceScheduler.Dispose();
    }

    private void OnSystemDisplaySettingsChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (_disposed || !_active || !_initialized)
            {
                return;
            }

            _pendingRebuild = true;
        }

        _debounceScheduler.Schedule(250, () => _uiDispatcher.BeginInvoke(ScheduleRebuildIfNeeded));
    }

    private void ScheduleRebuildIfNeeded()
    {
        long generation;
        lock (_gate)
        {
            if (_disposed || !_active || !_initialized || !_pendingRebuild || _rebuildInProgress)
            {
                return;
            }

            _rebuildInProgress = true;
            _pendingRebuild = false;
            generation = _rebuildGeneration;
        }

        _uiDispatcher.BeginInvoke(() => ExecuteRebuildOnUiThread(generation));
    }

    private void ExecuteRebuildOnUiThread(long generation)
    {
        try
        {
            lock (_gate)
            {
                if (_disposed || !_active || !_initialized || generation != _rebuildGeneration)
                {
                    return;
                }
            }

            DrawingSession? session;
            AppSettings? settings;
            Action? deactivateRequested;
            lock (_gate)
            {
                if (_disposed || !_active || !_initialized || generation != _rebuildGeneration)
                {
                    return;
                }

                session = _session;
                settings = _settings;
                deactivateRequested = _deactivateRequested;
            }

            if (session is null || settings is null || deactivateRequested is null)
            {
                return;
            }

            _onDisplayTopologyChanged();
            session.Clear();
            var monitors = MonitorEnumerator.EnumerateActiveMonitors();
            if (monitors.Count == 0)
            {
                DiagnosticLog.Write("DisplayTopology", "No monitors during rebuild.");
                _onRebuildFailed?.Invoke();
                return;
            }

            _overlayCoordinator.RebuildOverlays(session, settings, deactivateRequested);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("DisplayTopology", ex.GetType().Name);
            _notifyFailure?.Invoke("DisplayChangedTitle", "DisplayChangedMessage");
            _onRebuildFailed?.Invoke();
        }
        finally
        {
            FinishRebuildCycle();
        }
    }

    private void FinishRebuildCycle()
    {
        lock (_gate)
        {
            _rebuildInProgress = false;
            if (_disposed || !_active || !_initialized || !_pendingRebuild)
            {
                return;
            }
        }

        _debounceScheduler.Schedule(250, () => _uiDispatcher.BeginInvoke(ScheduleRebuildIfNeeded));
    }
}
