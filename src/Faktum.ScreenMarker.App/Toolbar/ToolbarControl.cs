using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.App.Settings;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Monitors;
using Faktum.ScreenMarker;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace Faktum.ScreenMarker.App.Toolbar;

public sealed class ToolbarControl : System.Windows.Controls.UserControl, IDisposable
{
    private readonly DrawingSession _session;
    private readonly AppSettings _settings;
    private readonly ToolbarInteractionCoordinator _coordinator;
    private readonly SettingsPersistenceCoordinator _persistence;
    private readonly ToolbarSelectionController _toolSelection;
    private readonly ToolbarColorSelectionController _colorSelection;
    private readonly ToolbarWidthSelectionController _widthSelection;
    private readonly ToolbarFontSizeSelectionController _fontSizeSelection;
    private MonitorInfo _monitor;
    private WpfButton? _undoButton;
    private WpfButton? _redoButton;
    private WpfButton? _clearButton;
    private bool _disposed;

    public ToolbarControl(
        DrawingSession session,
        AppSettings settings,
        SettingsPersistenceCoordinator persistence,
        ToolbarInteractionCoordinator coordinator,
        MonitorInfo monitor)
    {
        _session = session;
        _settings = settings;
        _coordinator = coordinator;
        _persistence = persistence;
        _monitor = monitor;
        _toolSelection = new ToolbarSelectionController(session, coordinator);
        _colorSelection = new ToolbarColorSelectionController(session, coordinator);
        _widthSelection = new ToolbarWidthSelectionController(session, coordinator);
        _fontSizeSelection = new ToolbarFontSizeSelectionController(coordinator);

        Background = new SolidColorBrush(WpfColor.FromArgb(230, 32, 32, 32));
        HorizontalAlignment = WpfHorizontalAlignment.Left;
        VerticalAlignment = WpfVerticalAlignment.Top;
        Content = BuildContent();
        Loaded += (_, _) => UpdateLayoutSize();
        _session.Changed += OnSessionChanged;
        Unloaded += (_, _) => DetachControllers();

        _coordinator.AttachToolbar(this);
        RefreshActionStates();
        SyncColorSelection();
        SyncWidthSelection();
        SyncFontSizeSelection();
    }

    internal SettingsPersistenceCoordinator SettingsPersistence => _persistence;

    internal DrawingTool SelectedTool => _toolSelection.SelectedTool;

    internal int RegisteredToolButtonCount => _toolSelection.RegisteredToolCount;

    internal int SelectedToolButtonCount => _toolSelection.SelectedButtonCount();

    internal bool IsToolButtonSelected(DrawingTool tool) => _toolSelection.IsToolSelected(tool);

    internal ToolbarInteractionCoordinator Coordinator => _coordinator;

    internal void UpdateMonitor(MonitorInfo monitor) => _monitor = monitor;

    internal void SyncToolSelection() => _toolSelection.SyncButtonStates();

    internal void SyncColorSelection() => _colorSelection.SyncSelection();

    internal void SyncWidthSelection() => _widthSelection.SyncSelection();

    internal void SyncFontSizeSelection() => _fontSizeSelection.SyncSelection();

    internal void RefreshActionStates()
    {
        if (_undoButton is not null)
        {
            _undoButton.IsEnabled = _session.History.CanUndo;
        }

        if (_redoButton is not null)
        {
            _redoButton.IsEnabled = _session.History.CanRedo;
        }

        if (_clearButton is not null)
        {
            _clearButton.IsEnabled = _session.Objects.Count > 0;
        }
    }

    internal void SetClearArmed(bool armed) =>
        _clearButton!.Content = armed ? "Clear?" : "Clear";

    internal FrameworkElement? FindControlByAutomationId(string automationId) =>
        FindControlByAutomationId(this, automationId);

    public void FlushSettings() => _persistence.Flush();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DetachControllers();
        GC.SuppressFinalize(this);
    }

    private void DetachControllers()
    {
        _session.Changed -= OnSessionChanged;
        _toolSelection.Detach();
        _colorSelection.Detach();
        _widthSelection.Detach();
        _fontSizeSelection.Detach();
    }

    private void OnSessionChanged() =>
        Dispatcher.BeginInvoke(RefreshActionStates);

    internal void EnsureMeasured()
    {
        UpdateLayoutSize();
        UpdateLayout();
    }

    private void UpdateLayoutSize()
    {
        Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        if (DesiredSize.Width > 0)
        {
            Width = DesiredSize.Width;
        }

        if (DesiredSize.Height > 0)
        {
            Height = DesiredSize.Height;
        }
    }

    private StackPanel BuildContent()
    {
        var panel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(6) };
        AddToolButton(panel, ToolbarShortcutLabels.ToolLabel(DrawingTool.Pen), DrawingTool.Pen, ToolbarControlIds.Tool.Pen);
        AddToolButton(panel, ToolbarShortcutLabels.ToolLabel(DrawingTool.Line), DrawingTool.Line, ToolbarControlIds.Tool.Line);
        AddToolButton(panel, ToolbarShortcutLabels.ToolLabel(DrawingTool.Arrow), DrawingTool.Arrow, ToolbarControlIds.Tool.Arrow);
        AddToolButton(panel, ToolbarShortcutLabels.ToolLabel(DrawingTool.Rectangle), DrawingTool.Rectangle, ToolbarControlIds.Tool.Rectangle);
        AddToolButton(panel, ToolbarShortcutLabels.ToolLabel(DrawingTool.Ellipse), DrawingTool.Ellipse, ToolbarControlIds.Tool.Ellipse);
        AddToolButton(panel, ToolbarShortcutLabels.ToolLabel(DrawingTool.Text), DrawingTool.Text, ToolbarControlIds.Tool.Text);
        AddToolButton(panel, ToolbarShortcutLabels.ToolLabel(DrawingTool.Eraser), DrawingTool.Eraser, ToolbarControlIds.Tool.Eraser);
        panel.Children.Add(new Separator { Width = 8 });

        var fontSizeCombo = new WpfComboBox
        {
            MinWidth = 52,
            Margin = new Thickness(2),
            ToolTip = AppApplication.GetString("ToolbarTextFontSize"),
        };
        AutomationProperties.SetAutomationId(fontSizeCombo, ToolbarControlIds.Text.FontSize);
        AutomationProperties.SetName(fontSizeCombo, ToolbarControlIds.Text.FontSize);
        _fontSizeSelection.Register(fontSizeCombo);
        panel.Children.Add(fontSizeCombo);
        panel.Children.Add(new Separator { Width = 8 });

        var colorDigit = 4;
        foreach (var color in ColorValue.DefaultPalette)
        {
            var button = new ToggleButton
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(WpfColor.FromArgb(color.A, color.R, color.G, color.B)),
                ToolTip = ToolbarShortcutLabels.ColorLabel(color, colorDigit),
            };
            colorDigit++;
            AutomationProperties.SetAutomationId(button, ToolbarControlIds.ColorIdFor(color));
            AutomationProperties.SetName(button, ToolbarControlIds.ColorIdFor(color));
            _colorSelection.Register(color, button);
            panel.Children.Add(button);
        }

        panel.Children.Add(new Separator { Width = 8 });
        var widthDigit = 1;
        foreach (var width in ToolbarWidthValues.SelectableWidths)
        {
            var button = new ToggleButton
            {
                Content = width.ToString("0", CultureInfo.InvariantCulture),
                Margin = new Thickness(2),
                MinWidth = 28,
                ToolTip = ToolbarShortcutLabels.WidthLabel(width, widthDigit),
            };
            widthDigit++;
            var automationId = ToolbarControlIds.WidthIdFor(width);
            AutomationProperties.SetAutomationId(button, automationId);
            AutomationProperties.SetName(button, automationId);
            _widthSelection.Register(width, button);
            panel.Children.Add(button);
        }

        panel.Children.Add(new Separator { Width = 8 });
        _undoButton = CreateActionButton("Undo", ToolbarControlIds.Action.Undo, () => _coordinator.Undo());
        _redoButton = CreateActionButton("Redo", ToolbarControlIds.Action.Redo, () => _coordinator.Redo());
        _clearButton = CreateActionButton("Clear", ToolbarControlIds.Action.Clear, () => _coordinator.HandleClearClick());
        panel.Children.Add(_undoButton);
        panel.Children.Add(_redoButton);
        panel.Children.Add(_clearButton);
        panel.Children.Add(CreateActionButton("Close", ToolbarControlIds.Action.Close, () => _coordinator.RequestClose()));
        return panel;
    }

    private void AddToolButton(WpfPanel panel, string label, DrawingTool tool, string automationId)
    {
        var button = new ToggleButton
        {
            Content = label,
            Margin = new Thickness(2),
            MinWidth = 48,
            Tag = tool,
        };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, automationId);
        _toolSelection.RegisterToolButton(tool, button);
        panel.Children.Add(button);
    }

    private static WpfButton CreateActionButton(string label, string automationId, Action action)
    {
        var button = new WpfButton { Content = label, Margin = new Thickness(2), MinWidth = 48, Tag = automationId };
        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(button, automationId);
        button.Click += (_, _) => action();
        return button;
    }

    private static FrameworkElement? FindControlByAutomationId(DependencyObject root, string automationId)
    {
        if (root is FrameworkElement element &&
            string.Equals(AutomationProperties.GetAutomationId(element), automationId, StringComparison.Ordinal))
        {
            return element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var found = FindControlByAutomationId(VisualTreeHelper.GetChild(root, i), automationId);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
