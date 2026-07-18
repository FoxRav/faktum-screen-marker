using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Faktum.ScreenMarker.App.Drawing;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.Monitors;
using Faktum.ScreenMarker.Platform.Windows.Windowing;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;

namespace Faktum.ScreenMarker.App.Overlays;

public sealed class OverlayWindow : Window
{
    private readonly MonitorInfo _monitor;
    private readonly DrawingSession _session;
    private readonly Grid _overlayRoot;
    private readonly OverlayInputSurface _inputSurface;
    private readonly ToolbarHost _toolbarHost;
    private readonly TextEditorHostLayer _textEditorLayer;
    private ToolbarInteractionCoordinator? _coordinator;
    private OverlayInputDiagnosticState _inputDiagnosticState;

    public MonitorInfo Monitor => _monitor;

    internal Grid OverlayRoot => _overlayRoot;

    internal OverlayInputSurface InputSurface => _inputSurface;

    internal ToolbarHost ToolbarHost => _toolbarHost;

    internal TextEditorHostLayer TextEditorLayer => _textEditorLayer;

    public OverlayInputDiagnosticState InputDiagnosticState => _inputDiagnosticState;

    public event Action? InteractionCompleted;

    public event Action? DrawingCommitFailed;

    public OverlayWindow(MonitorInfo monitor, DrawingSession session)
    {
        _monitor = monitor;
        _session = session;
        var drawingSurface = new DrawingSurface(monitor.DeviceName, session);
        _inputSurface = new OverlayInputSurface(drawingSurface);
        _toolbarHost = new ToolbarHost(monitor);
        _textEditorLayer = new TextEditorHostLayer();

        _overlayRoot = new Grid();
        System.Windows.Controls.Panel.SetZIndex(_inputSurface, OverlayLayerZIndex.InputSurface);
        _overlayRoot.Children.Add(_inputSurface);
        _overlayRoot.Children.Add(_toolbarHost);
        _overlayRoot.Children.Add(_textEditorLayer);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = new SolidColorBrush(WpfColor.FromArgb(OverlayInputSurface.InputCaptureAlpha, 0, 0, 0));
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = true;

        Content = _overlayRoot;
        _inputSurface.Cursor = System.Windows.Input.Cursors.Cross;

        _inputSurface.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        _inputSurface.PreviewMouseMove += OnPreviewMouseMove;
        _inputSurface.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        _inputSurface.PreviewMouseRightButtonDown += OnBlockNonLeftPointer;
        _inputSurface.PreviewMouseDown += OnPreviewMouseDownBlockMiddle;

        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => ReleaseInputCapture();
        SourceInitialized += (_, _) => UpdatePlacementForCurrentDpi();
        ContentRendered += (_, _) => ReapplyInputStylesBestEffort();
    }

    internal void BindInteractionCoordinator(ToolbarInteractionCoordinator coordinator)
    {
        if (_coordinator is not null)
        {
            _coordinator.InteractionCompleted -= ForwardInteractionCompleted;
            _coordinator.DrawingCommitFailed -= ForwardDrawingCommitFailed;
            _coordinator.UnregisterOverlay(this);
        }

        _coordinator = coordinator;
        coordinator.RegisterOverlay(this);
        coordinator.InteractionCompleted += ForwardInteractionCompleted;
        coordinator.DrawingCommitFailed += ForwardDrawingCommitFailed;
    }

    private void ForwardInteractionCompleted() => InteractionCompleted?.Invoke();

    private void ForwardDrawingCommitFailed() => DrawingCommitFailed?.Invoke();

    private void ReapplyInputStylesBestEffort()
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        _ = OverlayExtendedStyleVerifier.ApplyDrawingInputStyles(handle);
    }

    public bool InitializeInput()
    {
        try
        {
            return InitializeInputCore();
        }
        catch (Exception ex)
        {
            ReleaseInputCapture();
            DiagnosticLog.Write("OverlayInput", ex.GetType().Name);
            RecordInputDiagnostic(new System.Windows.Interop.WindowInteropHelper(this).Handle, false);
            return false;
        }
    }

    private bool InitializeInputCore()
    {
        var handle = EnsureWindowHandle();
        if (handle == 0)
        {
            RecordInputDiagnostic(0, false);
            return false;
        }

        EnsureLayoutReady();
        UpdatePlacementForCurrentDpi();
        EnsureLayoutReady();

        var styleResult = OverlayExtendedStyleVerifier.ApplyDrawingInputStyles(handle);
        var verified = OverlayExtendedStyleVerifier.VerifyNoClickThrough(handle);
        if (!verified.Success || verified.State.HasTransparent)
        {
            RecordInputDiagnostic(handle, false, verified.State.ExtendedStyle, verified.State);
            return false;
        }

        if (!VerifyInputSurfaceDimensions())
        {
            RecordInputDiagnostic(handle, false, verified.State.ExtendedStyle, verified.State);
            return false;
        }

        if (IsVisible)
        {
            Activate();
            Focus();
            _inputSurface.Focus();
        }

        RecordInputDiagnostic(handle, true, verified.State.ExtendedStyle, verified.State);
        return true;
    }

    public void ReleaseInputCapture()
    {
        _coordinator?.ReleaseCaptureFrom(_inputSurface);
        _coordinator?.PointerController.CancelDrawing(_session);
        if (_inputSurface.IsMouseCaptured)
        {
            _inputSurface.ReleaseMouseCapture();
        }

        _coordinator?.VerifyCaptureInvariant(_inputSurface);
    }

    public void UpdatePlacementForCurrentDpi()
    {
        Width = _monitor.Width / _monitor.DipScaleX;
        Height = _monitor.Height / _monitor.DipScaleY;
        Left = _monitor.Left / _monitor.DipScaleX;
        Top = _monitor.Top / _monitor.DipScaleY;

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        OverlayWindowPlacement.ApplyMonitorBounds(handle, _monitor);
        UpdateLayout();
    }

    private nint EnsureWindowHandle()
    {
        if (!IsLoaded)
        {
            Show();
        }

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        if (helper.Handle == 0)
        {
            helper.EnsureHandle();
        }

        UpdatePlacementForCurrentDpi();

        var handle = helper.Handle;
        if (handle != 0)
        {
            return handle;
        }

        var deadline = Environment.TickCount64 + 2000;
        while (handle == 0 && Environment.TickCount64 < deadline)
        {
            Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.Input);
            handle = helper.Handle;
        }

        return handle;
    }

    private void EnsureLayoutReady()
    {
        UpdateLayout();
        Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.Loaded);
        UpdateLayout();
        Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.Render);
        UpdateLayout();

        var width = Width > 0 ? Width : _monitor.Width / _monitor.DipScaleX;
        var height = Height > 0 ? Height : _monitor.Height / _monitor.DipScaleY;
        _overlayRoot.Measure(new System.Windows.Size(width, height));
        _overlayRoot.Arrange(new Rect(0, 0, width, height));
        _inputSurface.Measure(new System.Windows.Size(width, height));
        _inputSurface.Arrange(new Rect(0, 0, width, height));
        _toolbarHost.Measure(new System.Windows.Size(width, height));
        _toolbarHost.Arrange(new Rect(0, 0, width, height));
        _textEditorLayer.Measure(new System.Windows.Size(width, height));
        _textEditorLayer.Arrange(new Rect(0, 0, width, height));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdatePlacementForCurrentDpi();
    }

    private bool VerifyInputSurfaceDimensions()
    {
        var expectedWidth = Width > 0 ? Width : _monitor.Width / _monitor.DipScaleX;
        var expectedHeight = Height > 0 ? Height : _monitor.Height / _monitor.DipScaleY;
        var surfaceWidth = _inputSurface.ActualWidth > 0 ? _inputSurface.ActualWidth : expectedWidth;
        var surfaceHeight = _inputSurface.ActualHeight > 0 ? _inputSurface.ActualHeight : expectedHeight;
        if (surfaceWidth <= 0 || surfaceHeight <= 0)
        {
            return false;
        }

        const double tolerance = 2.0;
        return Math.Abs(surfaceWidth - expectedWidth) <= tolerance &&
               Math.Abs(surfaceHeight - expectedHeight) <= tolerance;
    }

    private void RecordInputDiagnostic(
        nint handle,
        bool initSucceeded,
        long extendedStyle = 0,
        OverlayExtendedStyleState? styleState = null)
    {
        var state = styleState ?? OverlayExtendedStyleState.FromExtendedStyle(extendedStyle);
        _inputDiagnosticState = new OverlayInputDiagnosticState(
            handle,
            _monitor.DeviceName,
            ActualWidth,
            ActualHeight,
            _inputSurface.ActualWidth,
            _inputSurface.ActualHeight,
            state.ExtendedStyle,
            state.HasTransparent,
            state.HasNoActivate,
            initSucceeded);
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_coordinator is null || !ReferenceEquals(sender, _inputSurface))
        {
            return;
        }

        e.Handled = true;
        try
        {
            var point = ToLocalDip(e.GetPosition(_inputSurface));
            _coordinator.HandlePointerDown(this, point, _inputSurface);
        }
        catch
        {
            ReleaseInputCapture();
            throw;
        }
    }

    private void OnPreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_coordinator is null || !ReferenceEquals(sender, _inputSurface))
        {
            return;
        }

        var state = _coordinator.State;
        if (state is not Core.Interaction.InteractionState.DragDrawing and not Core.Interaction.InteractionState.Erasing)
        {
            return;
        }

        e.Handled = true;
        var point = ToLocalDip(e.GetPosition(_inputSurface));
        var shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        _coordinator.HandlePointerMove(this, point, shift);
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_coordinator is null || !ReferenceEquals(sender, _inputSurface) || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (_coordinator.State is not Core.Interaction.InteractionState.DragDrawing and not Core.Interaction.InteractionState.Erasing)
        {
            _coordinator.VerifyCaptureInvariant(_inputSurface);
            return;
        }

        e.Handled = true;
        try
        {
            var point = ToLocalDip(e.GetPosition(_inputSurface));
            _coordinator.HandlePointerUp(this, point);
        }
        catch
        {
            ReleaseInputCapture();
            throw;
        }
    }

    private void OnBlockNonLeftPointer(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnPreviewMouseDownBlockMiddle(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (_coordinator is not null && _coordinator.HandleSessionKeyDown(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _coordinator?.HandleEscape();
            ReleaseInputCapture();
            e.Handled = true;
        }
    }

    private static Point2D ToLocalDip(WpfPoint position) => new(position.X, position.Y);
}
