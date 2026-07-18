using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.Core.Drawing;
using WpfColor = System.Windows.Media.Color;

namespace Faktum.ScreenMarker.App.Toolbar;

internal sealed class ToolbarColorSelectionController
{
    private readonly DrawingSession _session;
    private readonly ToolbarInteractionCoordinator _coordinator;
    private readonly Dictionary<ColorValue, ToggleButton> _buttons = new();
    private bool _syncing;

    public ToolbarColorSelectionController(DrawingSession session, ToolbarInteractionCoordinator coordinator)
    {
        _session = session;
        _coordinator = coordinator;
        _session.Changed += SyncFromSession;
    }

    public void Register(ColorValue color, ToggleButton button)
    {
        _buttons[color] = button;
        button.Checked += (_, _) => OnChecked(color);
        button.Unchecked += (_, _) => OnUnchecked(color);
        ApplyVisual(button, color, IsSelected(color));
    }

    public void Detach()
    {
        _session.Changed -= SyncFromSession;
        _buttons.Clear();
    }

    public void SyncSelection()
    {
        _syncing = true;
        try
        {
            foreach (var (color, button) in _buttons)
            {
                var selected = IsSelected(color);
                button.IsChecked = selected;
                ApplyVisual(button, color, selected);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private bool IsSelected(ColorValue color) =>
        color.R == _session.ActiveStyle.Color.R &&
        color.G == _session.ActiveStyle.Color.G &&
        color.B == _session.ActiveStyle.Color.B;

    private void OnChecked(ColorValue color)
    {
        if (_syncing)
        {
            return;
        }

        _coordinator.SelectColor(color);
        SyncSelection();
    }

    private void OnUnchecked(ColorValue color)
    {
        if (_syncing || IsSelected(color))
        {
            SyncSelection();
        }
    }

    private static void ApplyVisual(ToggleButton button, ColorValue color, bool selected)
    {
        button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
        if (selected)
        {
            button.BorderBrush = color == ColorValue.White || color == ColorValue.Yellow
                ? new SolidColorBrush(WpfColor.FromRgb(0, 120, 215))
                : System.Windows.Media.Brushes.White;
        }
        else
        {
            button.BorderBrush = color == ColorValue.Black
                ? new SolidColorBrush(WpfColor.FromRgb(120, 120, 120))
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    private void SyncFromSession() => SyncSelection();
}
