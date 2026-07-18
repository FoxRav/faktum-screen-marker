using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.App.Toolbar;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Interaction;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;

namespace Faktum.ScreenMarker.App.Interaction;

public sealed class ToolbarInteractionCoordinator : IDisposable
{
    private readonly DrawingSession _session;
    private readonly OverlayCoordinator _overlayCoordinator;
    private readonly SettingsPersistenceCoordinator _persistence;
    private readonly Action _deactivateRequested;
    private readonly ITextEditorHost _textEditorHost;
    private OverlayPointerInputController? _pointerController;
    private readonly HashSet<OverlayWindow> _overlays = [];
    private readonly DispatcherTimer _clearArmTimer;

    private ToolbarControl? _toolbar;
    private PointerInteractionLatch? _activeLatch;
    private UIElement? _captureOwner;
    private ITextEditorSession? _textEditor;
    private bool _textEditorCallbackFired;
    private Point2D _pendingTextOrigin;
    private bool _awaitingTextPlacement;
    private DrawingTool _persistentTool = DrawingTool.Pen;
    private DrawingTool? _textOneShotRestoreTool;
    private double _activeTextFontSize;
    private bool _clearArmed;
    private bool _disposed;

    public ToolbarInteractionCoordinator(
        DrawingSession session,
        OverlayCoordinator overlayCoordinator,
        SettingsPersistenceCoordinator persistence,
        Action deactivateRequested,
        ITextEditorHost? textEditorHost = null)
    {
        _session = session;
        _overlayCoordinator = overlayCoordinator;
        _persistence = persistence;
        _deactivateRequested = deactivateRequested;
        _textEditorHost = textEditorHost ?? new WpfTextEditorHost();
        _persistentTool = session.ActiveTool;
        _activeTextFontSize = ToolbarTextFontSizeValues.Normalize(persistence.Current.PreferredTextFontSize);
        _pointerController = new OverlayPointerInputController(session);
        _clearArmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _clearArmTimer.Tick += (_, _) => DisarmClear();
    }

    public InteractionState State { get; private set; } = InteractionState.Idle;

    public bool IsToolbarEnabled => State != InteractionState.Deactivating;

    public event Action? InteractionCompleted;

    public event Action? DrawingCommitFailed;

    internal DrawingTool PersistentTool => _persistentTool;

    internal bool IsAwaitingTextPlacement => _awaitingTextPlacement;

    internal double ActiveTextFontSize => _activeTextFontSize;

    internal OverlayPointerInputController PointerController =>
        _pointerController ?? throw new InvalidOperationException("Pointer controller is not initialized.");

    public void AttachToolbar(ToolbarControl toolbar) => _toolbar = toolbar;

    public void RegisterOverlay(OverlayWindow overlay)
    {
        _overlays.Add(overlay);
        PointerController.ConfigureMonitor(overlay.Monitor.DeviceName);
    }

    public void UnregisterOverlay(OverlayWindow overlay) => _overlays.Remove(overlay);

    public void SelectTool(DrawingTool tool)
    {
        EnsureNotDeactivating();
        if (State == InteractionState.TextEditing)
        {
            CloseTextEditor(committed: false);
        }

        CancelActiveInteraction(releaseCapture: true);
        DisarmClear();

        if (tool == DrawingTool.Text)
        {
            _textOneShotRestoreTool = _persistentTool;
            _awaitingTextPlacement = true;
            _session.ActiveTool = DrawingTool.Text;
            _toolbar?.SyncToolSelection();
            return;
        }

        _awaitingTextPlacement = false;
        _textOneShotRestoreTool = null;
        _persistentTool = tool;
        _session.ActiveTool = tool;
        _toolbar?.SyncToolSelection();
    }

    public void SelectColor(ColorValue color)
    {
        EnsureNotDeactivating();
        DisarmClear();
        _session.ActiveStyle = _session.ActiveStyle.WithWidth(_session.ActiveStyle.Width) with { Color = color };
        _persistence.UpdatePreferredColor(color);
        _toolbar?.SyncColorSelection();
    }

    public void SelectWidth(double width)
    {
        EnsureNotDeactivating();
        DisarmClear();
        var normalized = ToolbarWidthValues.Normalize(width);
        _session.ActiveStyle = _session.ActiveStyle.WithWidth(normalized);
        _persistence.UpdatePreferredStrokeWidth(normalized);
        _toolbar?.SyncWidthSelection();
    }

    public void SelectTextFontSize(double fontSize)
    {
        EnsureNotDeactivating();
        DisarmClear();
        var normalized = ToolbarTextFontSizeValues.Normalize(fontSize);
        _activeTextFontSize = normalized;
        _persistence.UpdatePreferredTextFontSize(normalized);
        _textEditor?.SetFontSize(normalized);
        _toolbar?.SyncFontSizeSelection();
    }

    public bool HandleSessionKeyDown(Key key, ModifierKeys modifiers) =>
        SessionKeyboardShortcuts.TryHandle(this, key, modifiers);

    public void Undo()
    {
        EnsureNotDeactivating();
        CancelActiveInteraction(releaseCapture: true);
        DisarmClear();
        _session.Undo();
        _toolbar?.RefreshActionStates();
    }

    public void Redo()
    {
        EnsureNotDeactivating();
        CancelActiveInteraction(releaseCapture: true);
        DisarmClear();
        _session.Redo();
        _toolbar?.RefreshActionStates();
    }

    public void HandleClearClick()
    {
        EnsureNotDeactivating();
        CancelActiveInteraction(releaseCapture: true);

        if (_session.Objects.Count == 0)
        {
            DisarmClear();
            return;
        }

        if (!_clearArmed)
        {
            _clearArmed = true;
            _clearArmTimer.Stop();
            _clearArmTimer.Start();
            _toolbar?.SetClearArmed(true);
            return;
        }

        _session.ClearAllWithHistory();
        DisarmClear();
        _toolbar?.RefreshActionStates();
    }

    public void RequestClose()
    {
        if (State == InteractionState.Deactivating)
        {
            return;
        }

        BeginDeactivation();
        _deactivateRequested();
    }

    public void HandlePointerDown(OverlayWindow source, Point2D point, UIElement captureTarget)
    {
        if (State == InteractionState.Deactivating || State == InteractionState.TextEditing)
        {
            return;
        }

        if (!ReferenceEquals(captureTarget, source.InputSurface))
        {
            return;
        }

        CancelActiveInteraction(releaseCapture: true);
        DisarmClear();

        var latch = PointerInteractionLatch.FromSession(_session, source.Monitor.DeviceName);
        _activeLatch = latch;
        PointerController.ConfigureMonitor(source.Monitor.DeviceName);

        switch (latch.Tool)
        {
            case DrawingTool.Text:
                _awaitingTextPlacement = false;
                OpenTextEditor(source, point, latch);
                return;
            case DrawingTool.Eraser:
                State = InteractionState.Erasing;
                AcquireCapture(captureTarget);
                PointerController.BeginInteraction(point, latch, _ => { });
                return;
            case DrawingTool.Pen or DrawingTool.Line or DrawingTool.Arrow or DrawingTool.Rectangle or DrawingTool.Ellipse:
                State = InteractionState.DragDrawing;
                AcquireCapture(captureTarget);
                PointerController.BeginInteraction(point, latch, _ => { });
                return;
            default:
                _activeLatch = null;
                break;
        }
    }

    public void HandlePointerMove(OverlayWindow source, Point2D point, bool shift)
    {
        if (State is InteractionState.DragDrawing or InteractionState.Erasing)
        {
            PointerController.UpdateDrag(point, shift);
        }
    }

    public void HandlePointerUp(OverlayWindow source, Point2D point)
    {
        if (State == InteractionState.Deactivating)
        {
            ReleaseCapture();
            return;
        }

        var committed = false;
        try
        {
            if (State == InteractionState.DragDrawing)
            {
                committed = PointerController.CompleteDrag(_session);
            }
            else if (State == InteractionState.Erasing)
            {
                PointerController.CompleteDrag(_session);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("PointerUp", ex.GetType().Name);
            PointerController.CancelDrawing(_session);
            DrawingCommitFailed?.Invoke();
        }
        finally
        {
            State = InteractionState.Idle;
            _activeLatch = null;
            ReleaseCapture();
            _toolbar?.RefreshActionStates();
        }

        if (committed)
        {
            InteractionCompleted?.Invoke();
        }
    }

    public void HandleEscape()
    {
        if (State == InteractionState.Deactivating)
        {
            return;
        }

        if (State == InteractionState.TextEditing)
        {
            CloseTextEditor(committed: false);
            return;
        }

        CancelActiveInteraction(releaseCapture: true);
        DisarmClear();
    }

    public void BeginDeactivation()
    {
        State = InteractionState.Deactivating;
        _clearArmTimer.Stop();
        CloseTextEditor(committed: false);
        CancelActiveInteraction(releaseCapture: true);
        DisarmClear();
        ReleaseCapture();
        _toolbar?.FlushSettings();
    }

    public void OnDisplayRebuild()
    {
        CloseTextEditor(committed: false);
        CancelActiveInteraction(releaseCapture: true);
        ReleaseCapture();
    }

    public void VerifyCaptureInvariant(UIElement? captureTarget)
    {
        if (State is InteractionState.DragDrawing or InteractionState.Erasing)
        {
            return;
        }

        if (captureTarget?.IsMouseCaptured == true)
        {
            DiagnosticLog.Write("CaptureInvariant", "Unexpected capture while idle.");
            captureTarget.ReleaseMouseCapture();
        }

        if (Mouse.Captured is not null && ReferenceEquals(Mouse.Captured, captureTarget))
        {
            captureTarget?.ReleaseMouseCapture();
        }
    }

    public void ReleaseCaptureFrom(UIElement? captureTarget)
    {
        if (captureTarget?.IsMouseCaptured == true)
        {
            captureTarget.ReleaseMouseCapture();
        }

        if (ReferenceEquals(_captureOwner, captureTarget))
        {
            _captureOwner = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clearArmTimer.Stop();
        CloseTextEditor(committed: false);
        ReleaseCapture();
    }

    private void OpenTextEditor(OverlayWindow overlay, Point2D origin, PointerInteractionLatch latch)
    {
        CloseTextEditor(committed: false);
        State = InteractionState.TextEditing;
        _textEditorCallbackFired = false;
        _pendingTextOrigin = origin;

        _textEditor = _textEditorHost.Open(
            overlay,
            origin,
            latch.Style,
            latch.MonitorDeviceName,
            _activeTextFontSize,
            result => CompleteTextEditor(result, latch));
    }

    private void CompleteTextEditor(TextEditorResult result, PointerInteractionLatch latch)
    {
        if (_textEditorCallbackFired)
        {
            return;
        }

        _textEditorCallbackFired = true;
        _textEditor = null;
        var committed = false;

        try
        {
            if (result.Committed && result.Text is not null)
            {
                var annotation = new TextAnnotation(
                    _session.AllocateId(),
                    latch.MonitorDeviceName,
                    latch.Style,
                    _pendingTextOrigin,
                    result.Text,
                    _activeTextFontSize);
                _session.BeginPreview(latch.MonitorDeviceName, annotation);
                committed = _session.CommitPreview();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("TextCommit", ex.GetType().Name);
            DrawingCommitFailed?.Invoke();
            committed = false;
        }
        finally
        {
            RestoreAfterText(committed);
        }
    }

    private void RestoreAfterText(bool committed)
    {
        State = InteractionState.Idle;
        RestorePersistentToolAfterText();
        _toolbar?.RefreshActionStates();

        if (committed)
        {
            InteractionCompleted?.Invoke();
        }
    }

    private void RestorePersistentToolAfterText()
    {
        var restore = _textOneShotRestoreTool ?? DrawingTool.Pen;
        _textOneShotRestoreTool = null;
        _awaitingTextPlacement = false;
        _persistentTool = restore;
        _session.ActiveTool = restore;
        _toolbar?.SyncToolSelection();
    }

    private void CloseTextEditor(bool committed)
    {
        if (_textEditor is null)
        {
            return;
        }

        if (committed)
        {
            return;
        }

        _textEditor.Cancel();
        _textEditor = null;
        if (State == InteractionState.TextEditing)
        {
            State = InteractionState.Idle;
            RestorePersistentToolAfterText();
        }
    }

    private void CancelActiveInteraction(bool releaseCapture)
    {
        if (State is InteractionState.DragDrawing or InteractionState.Erasing)
        {
            PointerController.CancelDrawing(_session);
            State = InteractionState.Idle;
            _activeLatch = null;
        }

        if (releaseCapture)
        {
            ReleaseCapture();
        }
    }

    private void AcquireCapture(UIElement captureTarget)
    {
        ReleaseCapture();
        _captureOwner = captureTarget;
        captureTarget.CaptureMouse();
    }

    private void ReleaseCapture()
    {
        if (_captureOwner?.IsMouseCaptured == true)
        {
            _captureOwner.ReleaseMouseCapture();
        }

        _captureOwner = null;
    }

    private void DisarmClear()
    {
        _clearArmTimer.Stop();
        if (!_clearArmed)
        {
            return;
        }

        _clearArmed = false;
        _toolbar?.SetClearArmed(false);
    }

    private void EnsureNotDeactivating()
    {
        if (State == InteractionState.Deactivating)
        {
            throw new InvalidOperationException("Toolbar actions are disabled during deactivation.");
        }
    }
}
