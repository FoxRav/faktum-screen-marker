using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.App.Toolbar;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Interaction;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Settings;
using WpfButton = System.Windows.Controls.Button;

namespace Faktum.ScreenMarker.Platform.Windows.Tests;

internal sealed class FakeTextEditorHost : ITextEditorHost
{
    public List<(Point2D Origin, double FontSize, Action<TextEditorResult> Callback)> Opens { get; } = [];

    public FakeTextEditorSession? LastSession { get; private set; }

    public ITextEditorSession Open(
        OverlayWindow overlay,
        Point2D origin,
        StrokeStyle style,
        string monitorDeviceName,
        double fontSize,
        Action<TextEditorResult> onCompleted)
    {
        _ = overlay;
        _ = style;
        _ = monitorDeviceName;
        Opens.Add((origin, fontSize, onCompleted));
        LastSession = new FakeTextEditorSession(onCompleted, fontSize);
        return LastSession;
    }
}

internal sealed class FakeTextEditorSession : ITextEditorSession
{
    private readonly Action<TextEditorResult> _onCompleted;
    private bool _completed;

    public FakeTextEditorSession(Action<TextEditorResult> onCompleted, double fontSize) =>
        (_onCompleted, FontSize) = (onCompleted, fontSize);

    public double FontSize { get; private set; }

    public bool IsOpen => !_completed;

    public void FocusEditor()
    {
    }

    public void SetFontSize(double fontSize) => FontSize = fontSize;

    public void Cancel() => Complete(new TextEditorResult(false, null));

    public void Commit(string text) => Complete(new TextEditorResult(true, text));

    private void Complete(TextEditorResult result)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _onCompleted(result);
    }
}

[Collection("WpfSta")]
public class ToolbarControlMatrixTests
{
    public static IEnumerable<object[]> AllControlIds() =>
        OverlayVisualHitTesting.AllToolbarControlAutomationIds.Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(AllControlIds))]
    public void ControlExistsOnceAndIsVisible(string automationId)
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            var matches = harness.FindAllByAutomationId(automationId);
            Assert.Single(matches);
            Assert.True(matches[0].IsVisible);
        });
    }

    [Fact]
    public void RoutedInputUpdatesToolSelectionBeforeDrawing()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            WpfTestContext.Pump();
            var overlay = harness.PointerOverlay;
            var line = harness.RequireControl<ToggleButton>(ToolbarControlIds.Tool.Line);
            ToolbarRoutedInput.InvokeAtCenter(line, overlay.OverlayRoot);
            WpfTestContext.Pump();
            Assert.Equal(DrawingTool.Line, harness.Session.ActiveTool);
            Assert.Equal(1, harness.Toolbar.SelectedToolButtonCount);
            Assert.True(harness.Coordinator.VerifyToolbarHitTesting());
            Assert.Null(Mouse.Captured);
        });
    }

    [Fact]
    public void EraserEmptyClickDoesNotLeaveCapture()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<ToggleButton>(ToolbarControlIds.Tool.Eraser),
                harness.PointerOverlay.OverlayRoot);
            WpfTestContext.Pump();

            var overlay = harness.Coordinator.Overlays[0];
            overlay.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            });
            overlay.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent,
            });
            WpfTestContext.Pump();

            harness.Interaction.VerifyCaptureInvariant(overlay.InputSurface);
            Assert.Null(Mouse.Captured);
        });
    }

    [Theory]
    [MemberData(nameof(AllControlIds))]
    public void RoutedInputHitsExpectedControl(string automationId)
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            WpfTestContext.Pump();
            var control = harness.RequireControl<FrameworkElement>(automationId);
            Assert.True(
                OverlayVisualHitTesting.HitTestControlCenter(harness.PointerOverlay.OverlayRoot, control, out var failedId),
                failedId);
            if (control is ToggleButton or WpfButton)
            {
                if (automationId == ToolbarControlIds.Action.Close)
                {
                    return;
                }

                ToolbarRoutedInput.InvokeAtCenter(control, harness.PointerOverlay.OverlayRoot);
                WpfTestContext.Pump();
            }

            Assert.True(harness.Coordinator.VerifyToolbarHitTesting());
            Assert.Null(Mouse.Captured);
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("WpfSta")]
public class ToolbarInteractionCoordinatorTests
{
    [Fact]
    public void TextOneShotRestoresPersistentToolAfterCommit()
    {
        RunSta(() =>
        {
            var fakeText = new FakeTextEditorHost();
            using var harness = ToolbarTestHarness.Create(fakeText);
            harness.Interaction.SelectTool(DrawingTool.Line);
            harness.Interaction.SelectTool(DrawingTool.Text);

            var overlay = harness.Coordinator.Overlays[0];
            harness.Interaction.HandlePointerDown(overlay, new Point2D(20, 20), overlay.InputSurface);
            WpfTestContext.Pump();

            Assert.Equal(InteractionState.TextEditing, harness.Interaction.State);
            fakeText.LastSession!.Commit("hello");
            WpfTestContext.Pump();

            Assert.Equal(DrawingTool.Line, harness.Session.ActiveTool);
            Assert.Equal(InteractionState.Idle, harness.Interaction.State);
            Assert.Single(harness.Session.Objects);
            Assert.True(harness.Coordinator.VerifyToolbarHitTesting());
        });
    }

    [Fact]
    public void TextCancelRestoresPenAndKeepsToolbarClickable()
    {
        RunSta(() =>
        {
            var fakeText = new FakeTextEditorHost();
            using var harness = ToolbarTestHarness.Create(fakeText);
            harness.Interaction.SelectTool(DrawingTool.Text);
            var overlay = harness.Coordinator.Overlays[0];
            harness.Interaction.HandlePointerDown(overlay, new Point2D(10, 10), overlay.InputSurface);
            fakeText.LastSession!.Cancel();
            WpfTestContext.Pump();

            Assert.Equal(DrawingTool.Pen, harness.Session.ActiveTool);
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<ToggleButton>(ToolbarControlIds.Tool.Arrow),
                harness.PointerOverlay.OverlayRoot);
            Assert.Equal(DrawingTool.Arrow, harness.Session.ActiveTool);
        });
    }

    [Fact]
    public void EraserDragRemovesMultipleObjectsWithoutDuplicateHistory()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            var monitor = harness.Coordinator.Overlays[0].Monitor.DeviceName;
            harness.Session.BeginPreview(monitor, new LineAnnotation(harness.Session.AllocateId(), monitor, harness.Session.ActiveStyle, new Point2D(0, 0), new Point2D(10, 0)));
            harness.Session.CommitPreview();
            harness.Session.BeginPreview(monitor, new LineAnnotation(harness.Session.AllocateId(), monitor, harness.Session.ActiveStyle, new Point2D(0, 5), new Point2D(10, 5)));
            harness.Session.CommitPreview();
            harness.Interaction.SelectTool(DrawingTool.Eraser);

            var overlay = harness.Coordinator.Overlays[0];
            harness.Interaction.HandlePointerDown(overlay, new Point2D(5, 0), overlay.InputSurface);
            harness.Interaction.HandlePointerMove(overlay, new Point2D(5, 5), shift: false);
            harness.Interaction.HandlePointerUp(overlay, new Point2D(5, 5));
            WpfTestContext.Pump();

            Assert.Empty(harness.Session.Objects);
            Assert.True(harness.Session.History.UndoCount >= 2);
            Assert.Null(Mouse.Captured);
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("WpfSta")]
public class UninterruptedToolbarSessionTests
{
    [Fact]
    public void NineteenStepSessionScenarioWithHitTestInvariant()
    {
        RunSta(() =>
        {
            var fakeText = new FakeTextEditorHost();
            using var harness = ToolbarTestHarness.Create(fakeText);
            var monitor = harness.Coordinator.Overlays[0].Monitor.DeviceName;
            var overlay = harness.Coordinator.Overlays[0];

            AssertHitTest(harness);
            Assert.Equal(DrawingTool.Pen, harness.Session.ActiveTool);
            DrawPenStroke(harness, overlay, monitor, 1);
            AssertHitTest(harness);
            InvokeTool(harness, ToolbarControlIds.Tool.Line);
            DrawLine(harness, overlay, monitor, 2);
            AssertHitTest(harness);
            InvokeTool(harness, ToolbarControlIds.Tool.Arrow);
            DrawLine(harness, overlay, monitor, 3);
            AssertHitTest(harness);
            InvokeTool(harness, ToolbarControlIds.Tool.Rectangle);
            DrawLine(harness, overlay, monitor, 4);
            AssertHitTest(harness);
            InvokeTool(harness, ToolbarControlIds.Tool.Ellipse);
            DrawLine(harness, overlay, monitor, 5);
            AssertHitTest(harness);
            InvokeTool(harness, ToolbarControlIds.Tool.Pen);
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<ToggleButton>(ToolbarControlIds.Color.Blue),
                harness.PointerOverlay.OverlayRoot);
            AssertHitTest(harness);
            DrawPenStroke(harness, overlay, monitor, 6);
            AssertHitTest(harness);
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<ToggleButton>(ToolbarControlIds.Width.W4),
                harness.PointerOverlay.OverlayRoot);
            AssertHitTest(harness);
            DrawPenStroke(harness, overlay, monitor, 7);
            AssertHitTest(harness);
            InvokeTool(harness, ToolbarControlIds.Tool.Text);
            harness.Interaction.HandlePointerDown(overlay, new Point2D(40, 40), overlay.InputSurface);
            fakeText.LastSession!.Commit("label");
            WpfTestContext.Pump();
            AssertHitTest(harness);
            Assert.Equal(DrawingTool.Pen, harness.Session.ActiveTool);
            harness.Toolbar.RefreshActionStates();
            if (harness.Session.History.CanUndo)
            {
                ToolbarRoutedInput.InvokeAtCenter(
                    harness.RequireControl<WpfButton>(ToolbarControlIds.Action.Undo),
                    harness.PointerOverlay.OverlayRoot);
                WpfTestContext.Pump();
                harness.Toolbar.RefreshActionStates();
                AssertHitTest(harness);
            }

            if (harness.Session.History.CanRedo)
            {
                ToolbarRoutedInput.InvokeAtCenter(
                    harness.RequireControl<WpfButton>(ToolbarControlIds.Action.Redo),
                    harness.PointerOverlay.OverlayRoot);
                WpfTestContext.Pump();
                AssertHitTest(harness);
            }

            InvokeTool(harness, ToolbarControlIds.Tool.Eraser);
            harness.Interaction.HandlePointerDown(overlay, new Point2D(5, 5), overlay.InputSurface);
            harness.Interaction.HandlePointerUp(overlay, new Point2D(5, 5));
            AssertHitTest(harness);
            harness.Interaction.HandlePointerDown(overlay, new Point2D(999, 999), overlay.InputSurface);
            harness.Interaction.HandlePointerUp(overlay, new Point2D(999, 999));
            AssertHitTest(harness);
            InvokeTool(harness, ToolbarControlIds.Tool.Pen);
            DrawPenStroke(harness, overlay, monitor, 8);
            AssertHitTest(harness);
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<WpfButton>(ToolbarControlIds.Action.Clear),
                harness.PointerOverlay.OverlayRoot);
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<WpfButton>(ToolbarControlIds.Action.Clear),
                harness.PointerOverlay.OverlayRoot);
            AssertHitTest(harness);
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<WpfButton>(ToolbarControlIds.Action.Undo),
                harness.PointerOverlay.OverlayRoot);
            AssertHitTest(harness);
            harness.Interaction.VerifyCaptureInvariant(overlay.InputSurface);
            Assert.Null(Mouse.Captured);

            if (harness.Coordinator.Overlays.Count > 1)
            {
                var second = harness.Coordinator.Overlays[1];
                DrawPenStroke(harness, second, second.Monitor.DeviceName, 9);
            }
        });
    }

    private static void AssertHitTest(ToolbarTestHarness harness) =>
        Assert.True(harness.Coordinator.VerifyToolbarHitTesting());

    private static void InvokeTool(ToolbarTestHarness harness, string automationId)
    {
        ToolbarRoutedInput.InvokeAtCenter(harness.RequireControl<ToggleButton>(automationId), harness.PointerOverlay.OverlayRoot);
        WpfTestContext.Pump();
    }

    private static void DrawPenStroke(ToolbarTestHarness harness, OverlayWindow overlay, string monitor, int seed)
    {
        harness.Interaction.HandlePointerDown(overlay, new Point2D(seed, seed), overlay.InputSurface);
        harness.Interaction.HandlePointerMove(overlay, new Point2D(seed + 20, seed + 10), shift: false);
        harness.Interaction.HandlePointerUp(overlay, new Point2D(seed + 20, seed + 10));
        WpfTestContext.Pump();
        _ = monitor;
    }

    private static void DrawLine(ToolbarTestHarness harness, OverlayWindow overlay, string monitor, int seed)
    {
        harness.Interaction.HandlePointerDown(overlay, new Point2D(seed, seed), overlay.InputSurface);
        harness.Interaction.HandlePointerMove(overlay, new Point2D(seed + 15, seed + 15), shift: false);
        harness.Interaction.HandlePointerUp(overlay, new Point2D(seed + 15, seed + 15));
        WpfTestContext.Pump();
        _ = monitor;
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("WpfSta")]
public class Kone1DualMonitorScenarioTests
{
    [Fact]
    public void DualMonitorSharedToolSelectionDocumentedScenario()
    {
        var monitors = Faktum.ScreenMarker.Platform.Windows.Monitors.MonitorEnumerator.EnumerateActiveMonitors();
        if (monitors.Count < 2)
        {
            return;
        }

        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            Assert.Equal(2, harness.Coordinator.Overlays.Count);
            ToolbarRoutedInput.InvokeAtCenter(
                harness.RequireControl<ToggleButton>(ToolbarControlIds.Tool.Arrow),
                harness.PointerOverlay.OverlayRoot);
            var first = harness.Coordinator.Overlays[0].Monitor.DeviceName;
            var second = harness.Coordinator.Overlays[1].Monitor.DeviceName;
            harness.Session.BeginPreview(first, new ArrowAnnotation(harness.Session.AllocateId(), first, harness.Session.ActiveStyle, new Point2D(1, 1), new Point2D(4, 4)));
            harness.Session.CommitPreview();
            harness.Session.BeginPreview(second, new ArrowAnnotation(harness.Session.AllocateId(), second, harness.Session.ActiveStyle, new Point2D(2, 2), new Point2D(5, 5)));
            harness.Session.CommitPreview();
            harness.Coordinator.RebuildOverlays(harness.Session, harness.Settings, () => { });
            Assert.Equal(DrawingTool.Arrow, harness.Session.ActiveTool);
            Assert.True(harness.Coordinator.VerifyToolbarHitTesting());
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

internal sealed class ToolbarTestHarness : IDisposable
{
    private readonly OverlayCoordinator _coordinator;
    private readonly SettingsPersistenceCoordinator _persistence;

    private ToolbarTestHarness(
        OverlayCoordinator coordinator,
        DrawingSession session,
        AppSettings settings,
        SettingsPersistenceCoordinator persistence,
        ToolbarInteractionCoordinator interaction,
        string tempDirectory)
    {
        _coordinator = coordinator;
        Session = session;
        Settings = settings;
        _persistence = persistence;
        Interaction = interaction;
        TempDirectory = tempDirectory;
        Toolbar = coordinator.Toolbar!;
        PointerOverlay = coordinator.Overlays.First(overlay => overlay.ToolbarHost.Toolbar is not null);
    }

    public DrawingSession Session { get; }

    public AppSettings Settings { get; }

    public OverlayCoordinator Coordinator => _coordinator;

    public ToolbarInteractionCoordinator Interaction { get; }

    public ToolbarControl Toolbar { get; }

    public OverlayWindow PointerOverlay { get; }

    public string TempDirectory { get; }

    public static ToolbarTestHarness Create(ITextEditorHost? textEditorHost = null)
    {
        var coordinator = new OverlayCoordinator();
        var session = new DrawingSession();
        var settings = AppSettings.CreateDefault();
        var temp = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        SettingsService.UseStoreForTesting(new JsonFileSettingsStore(temp));
        var persistence = new SettingsPersistenceCoordinator(settings);
        Assert.True(coordinator.Activate(session, settings, persistence, () => coordinator.Deactivate(), textEditorHost));
        return new ToolbarTestHarness(coordinator, session, settings, persistence, coordinator.InteractionCoordinator!, temp);
    }

    public T RequireControl<T>(string automationId)
        where T : FrameworkElement
    {
        var control = Toolbar.FindControlByAutomationId(automationId);
        Assert.NotNull(control);
        return Assert.IsAssignableFrom<T>(control);
    }

    public List<FrameworkElement> FindAllByAutomationId(string automationId)
    {
        var results = new List<FrameworkElement>();
        CollectByAutomationId(Toolbar, automationId, results);
        return results;
    }

    private static void CollectByAutomationId(DependencyObject root, string automationId, List<FrameworkElement> results)
    {
        if (root is FrameworkElement element &&
            string.Equals(AutomationProperties.GetAutomationId(element), automationId, StringComparison.Ordinal))
        {
            results.Add(element);
        }

        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            CollectByAutomationId(System.Windows.Media.VisualTreeHelper.GetChild(root, i), automationId, results);
        }
    }

    public void Dispose()
    {
        _coordinator.Deactivate();
        Session.Dispose();
        _persistence.Dispose();
        SettingsService.ResetStoreForTesting();
        if (Directory.Exists(TempDirectory))
        {
            Directory.Delete(TempDirectory, recursive: true);
        }
    }
}
