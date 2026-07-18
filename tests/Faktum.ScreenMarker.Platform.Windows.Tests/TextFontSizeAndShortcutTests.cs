using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.App.Toolbar;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Interaction;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Settings;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfKeyboard = System.Windows.Input.Keyboard;

namespace Faktum.ScreenMarker.Platform.Windows.Tests;

[Collection("WpfSta")]
public class TextFontSizeInteractionTests
{
    [Fact]
    public void CommitStoresFontSizeOnTextAnnotation()
    {
        RunSta(() =>
        {
            var fakeText = new FakeTextEditorHost();
            using var harness = ToolbarTestHarness.Create(fakeText);
            harness.Interaction.SelectTextFontSize(48);
            harness.Interaction.SelectTool(DrawingTool.Text);

            var overlay = harness.Coordinator.Overlays[0];
            harness.Interaction.HandlePointerDown(overlay, new Point2D(20, 20), overlay.InputSurface);
            WpfTestContext.Pump();

            Assert.Equal(48, fakeText.LastSession!.FontSize);
            fakeText.LastSession.Commit("hello");
            WpfTestContext.Pump();

            var text = Assert.IsType<TextAnnotation>(Assert.Single(harness.Session.Objects));
            Assert.Equal(48, text.FontSize);
        });
    }

    [Fact]
    public void ChangingSizeDoesNotAlterExistingTextObjects()
    {
        RunSta(() =>
        {
            var fakeText = new FakeTextEditorHost();
            using var harness = ToolbarTestHarness.Create(fakeText);
            harness.Interaction.SelectTextFontSize(32);
            harness.Interaction.SelectTool(DrawingTool.Text);
            var overlay = harness.Coordinator.Overlays[0];
            harness.Interaction.HandlePointerDown(overlay, new Point2D(10, 10), overlay.InputSurface);
            fakeText.LastSession!.Commit("first");
            WpfTestContext.Pump();

            harness.Interaction.SelectTextFontSize(64);
            harness.Interaction.SelectTool(DrawingTool.Text);
            harness.Interaction.HandlePointerDown(overlay, new Point2D(30, 30), overlay.InputSurface);
            fakeText.LastSession!.Commit("second");
            WpfTestContext.Pump();

            var objects = harness.Session.Objects.OfType<TextAnnotation>().OrderBy(text => text.Origin.X).ToList();
            Assert.Equal(2, objects.Count);
            Assert.Equal(32, objects[0].FontSize);
            Assert.Equal(64, objects[1].FontSize);
        });
    }

    [Fact]
    public void OpenEditorUpdatesWhenToolbarSizeChanges()
    {
        RunSta(() =>
        {
            var fakeText = new FakeTextEditorHost();
            using var harness = ToolbarTestHarness.Create(fakeText);
            harness.Interaction.SelectTextFontSize(24);
            harness.Interaction.SelectTool(DrawingTool.Text);
            var overlay = harness.Coordinator.Overlays[0];
            harness.Interaction.HandlePointerDown(overlay, new Point2D(10, 10), overlay.InputSurface);
            WpfTestContext.Pump();

            harness.Interaction.SelectTextFontSize(96);
            WpfTestContext.Pump();

            Assert.Equal(96, fakeText.LastSession!.FontSize);
            fakeText.LastSession.Cancel();
        });
    }

    [Fact]
    public void TextSizeSelectionDoesNotChangeActiveToolOrStrokeWidth()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            harness.Interaction.SelectTool(DrawingTool.Line);
            harness.Interaction.SelectWidth(8);
            var widthBefore = harness.Session.ActiveStyle.Width;

            harness.Interaction.SelectTextFontSize(48);

            Assert.Equal(DrawingTool.Line, harness.Session.ActiveTool);
            Assert.Equal(widthBefore, harness.Session.ActiveStyle.Width);
            Assert.Equal(48, harness.Interaction.ActiveTextFontSize);
        });
    }

    [Fact]
    public void FontSizeSelectorExistsWithAutomationId()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            var control = harness.RequireControl<WpfComboBox>(ToolbarControlIds.Text.FontSize);
            Assert.True(control.IsVisible);
            Assert.Equal("24", control.SelectedItem as string);
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("WpfSta")]
public class SessionKeyboardShortcutTests
{
    public static IEnumerable<object[]> ToolShortcutCases() =>
    [
        [Key.Q, DrawingTool.Pen],
        [Key.W, DrawingTool.Line],
        [Key.E, DrawingTool.Arrow],
        [Key.A, DrawingTool.Rectangle],
        [Key.S, DrawingTool.Ellipse],
        [Key.X, DrawingTool.Eraser],
    ];

    [Theory]
    [MemberData(nameof(ToolShortcutCases))]
    public void ToolShortcutsSelectExpectedTool(Key key, DrawingTool expectedTool)
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            RaiseShortcut(harness.Coordinator.Overlays[0], key);
            WpfTestContext.Pump();
            Assert.Equal(expectedTool, harness.Session.ActiveTool);
            Assert.True(harness.Toolbar.IsToolButtonSelected(expectedTool));
        });
    }

    [Fact]
    public void TextShortcutIsOneShot()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            harness.Interaction.SelectTool(DrawingTool.Line);
            RaiseShortcut(harness.Coordinator.Overlays[0], Key.Z);
            WpfTestContext.Pump();
            Assert.True(harness.Interaction.IsAwaitingTextPlacement);
            Assert.Equal(DrawingTool.Text, harness.Session.ActiveTool);
        });
    }

    [Theory]
    [InlineData(Key.D1, 2.0)]
    [InlineData(Key.D2, 4.0)]
    [InlineData(Key.D3, 8.0)]
    public void WidthShortcutsSelectExpectedWidth(Key key, double expectedWidth)
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            RaiseShortcut(harness.Coordinator.Overlays[0], key);
            WpfTestContext.Pump();
            Assert.Equal(expectedWidth, harness.Session.ActiveStyle.Width);
        });
    }

    [Theory]
    [InlineData(Key.D4, 255, 0, 0)]
    [InlineData(Key.D5, 0, 180, 0)]
    [InlineData(Key.D6, 0, 102, 204)]
    [InlineData(Key.D7, 255, 204, 0)]
    [InlineData(Key.D8, 255, 255, 255)]
    [InlineData(Key.D9, 0, 0, 0)]
    public void ColorShortcutsSelectExpectedColor(Key key, byte r, byte g, byte b)
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            RaiseShortcut(harness.Coordinator.Overlays[0], key);
            WpfTestContext.Pump();
            Assert.Equal(r, harness.Session.ActiveStyle.Color.R);
            Assert.Equal(g, harness.Session.ActiveStyle.Color.G);
            Assert.Equal(b, harness.Session.ActiveStyle.Color.B);
        });
    }

    [Fact]
    public void ShortcutsBlockedDuringTextEditing()
    {
        RunSta(() =>
        {
            var fakeText = new FakeTextEditorHost();
            using var harness = ToolbarTestHarness.Create(fakeText);
            harness.Interaction.SelectTool(DrawingTool.Text);
            var overlay = harness.Coordinator.Overlays[0];
            harness.Interaction.HandlePointerDown(overlay, new Point2D(10, 10), overlay.InputSurface);
            WpfTestContext.Pump();

            RaiseShortcut(overlay, Key.Q);
            WpfTestContext.Pump();

            Assert.Equal(InteractionState.TextEditing, harness.Interaction.State);
            Assert.Equal(DrawingTool.Text, harness.Session.ActiveTool);
            fakeText.LastSession!.Cancel();
        });
    }

    [Fact]
    public void ShortcutOnSecondMonitorUsesSharedCoordinator()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            if (harness.Coordinator.Overlays.Count < 2)
            {
                return;
            }

            var second = harness.Coordinator.Overlays[1];
            RaiseShortcut(second, Key.W);
            WpfTestContext.Pump();
            Assert.Equal(DrawingTool.Line, harness.Session.ActiveTool);
            Assert.True(harness.Toolbar.IsToolButtonSelected(DrawingTool.Line));
        });
    }

    [Fact]
    public void ColorShortcutDoesNotChangeTextFontSizeOrStrokeWidth()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            harness.Interaction.SelectTextFontSize(32);
            harness.Interaction.SelectWidth(2);
            RaiseShortcut(harness.Coordinator.Overlays[0], Key.D5);
            WpfTestContext.Pump();
            Assert.Equal(32, harness.Interaction.ActiveTextFontSize);
            Assert.Equal(2, harness.Session.ActiveStyle.Width);
        });
    }

    [Fact]
    public void CtrlModifiedShortcutIsIgnored()
    {
        RunSta(() =>
        {
            using var harness = ToolbarTestHarness.Create();
            harness.Interaction.SelectTool(DrawingTool.Line);
            var handled = harness.Interaction.HandleSessionKeyDown(Key.Q, ModifierKeys.Control);
            WpfTestContext.Pump();
            Assert.False(handled);
            Assert.Equal(DrawingTool.Line, harness.Session.ActiveTool);
        });
    }

    private static void RaiseShortcut(OverlayWindow overlay, Key key)
    {
        overlay.RaiseEvent(new KeyEventArgs(
            WpfKeyboard.PrimaryDevice,
            PresentationSource.FromVisual(overlay),
            0,
            key)
        {
            RoutedEvent = UIElement.PreviewKeyDownEvent,
        });
    }

    private static void RunSta(Action action) => StaTestHost.Run(action);
}

[Collection("SettingsStore")]
public class TextFontSizeSettingsTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _productionDirectory;

    public TextFontSizeSettingsTests()
    {
        _productionDirectory = SettingsPaths.SettingsDirectory;
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FaktumScreenMarkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        SettingsService.UseStoreForTesting(new JsonFileSettingsStore(_tempDirectory));
    }

    public void Dispose()
    {
        SettingsService.ResetStoreForTesting();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void PreferredTextFontSizeRoundTrip()
    {
        var settings = AppSettings.CreateDefault();
        settings.PreferredTextFontSize = 48;
        SettingsService.Save(settings);
        var loaded = SettingsService.Load();
        Assert.Equal(48, loaded.PreferredTextFontSize);
    }

    [Fact]
    public void InvalidTextFontSizeSnapsToNearestPreset()
    {
        var settings = AppSettings.CreateDefault();
        settings.PreferredTextFontSize = 30;
        SettingsService.Save(settings);
        var loaded = SettingsService.Load();
        Assert.Equal(32, loaded.PreferredTextFontSize);
    }

    [Fact]
    public void ProductionSettingsPathIsNotUsedByTests()
    {
        Assert.NotEqual(_productionDirectory, SettingsService.Store.SettingsDirectory);
    }
}

public class TextFontSizeValuesTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(96)]
    public void NormalizeKeepsPresets(double size) =>
        Assert.Equal(size, TextFontSizeValues.Normalize(size));

    [Fact]
    public void InvalidValueFallsBackToDefault() =>
        Assert.Equal(TextFontSizeValues.InvalidFallback, TextFontSizeValues.Normalize(17));

    [Theory]
    [InlineData(17, 16)]
    [InlineData(30, 32)]
    [InlineData(100, 96)]
    public void ValidateOnLoadSnapsToNearest(double input, double expected) =>
        Assert.Equal(expected, TextFontSizeValues.ValidateOnLoad(input));
}
