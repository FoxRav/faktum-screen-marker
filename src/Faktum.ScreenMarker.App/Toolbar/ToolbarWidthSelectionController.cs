using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.App.Toolbar;

internal sealed class ToolbarWidthSelectionController
{
    private readonly DrawingSession _session;
    private readonly ToolbarInteractionCoordinator _coordinator;
    private readonly Dictionary<double, ToggleButton> _buttons = new();
    private bool _syncing;

    public ToolbarWidthSelectionController(DrawingSession session, ToolbarInteractionCoordinator coordinator)
    {
        _session = session;
        _coordinator = coordinator;
        _session.Changed += SyncFromSession;
    }

    public void Register(double width, ToggleButton button)
    {
        _buttons[width] = button;
        button.Checked += (_, _) => OnChecked(width);
        button.Unchecked += (_, _) => OnUnchecked(width);
        ApplyVisual(button, IsSelected(width));
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
            var selectedWidth = ToolbarWidthValues.Normalize(_session.ActiveStyle.Width);
            foreach (var (width, button) in _buttons)
            {
                var selected = Math.Abs(width - selectedWidth) < 0.001;
                button.IsChecked = selected;
                ApplyVisual(button, selected);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private bool IsSelected(double width) =>
        Math.Abs(ToolbarWidthValues.Normalize(_session.ActiveStyle.Width) - width) < 0.001;

    private void OnChecked(double width)
    {
        if (_syncing)
        {
            return;
        }

        _coordinator.SelectWidth(width);
        SyncSelection();
    }

    private void OnUnchecked(double width)
    {
        if (_syncing || IsSelected(width))
        {
            SyncSelection();
        }
    }

    private static void ApplyVisual(ToggleButton button, bool selected)
    {
        button.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
        button.BorderBrush = selected ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;
    }

    private void SyncFromSession() => SyncSelection();
}
