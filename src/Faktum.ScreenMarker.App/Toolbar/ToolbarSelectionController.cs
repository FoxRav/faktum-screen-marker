using System.Windows.Controls.Primitives;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.App.Toolbar;

internal sealed class ToolbarSelectionController
{
    private readonly DrawingSession _session;
    private readonly ToolbarInteractionCoordinator _coordinator;
    private readonly Dictionary<DrawingTool, ToggleButton> _toolButtons = new();
    private bool _syncing;

    public ToolbarSelectionController(DrawingSession session, ToolbarInteractionCoordinator coordinator)
    {
        _session = session;
        _coordinator = coordinator;
        _session.Changed += SyncFromSession;
    }

    public int RegisteredToolCount => _toolButtons.Count;

    public DrawingTool SelectedTool => _session.ActiveTool;

    public void RegisterToolButton(DrawingTool tool, ToggleButton button)
    {
        _toolButtons[tool] = button;
        button.IsChecked = IsToolVisuallySelected(tool);
        button.Checked += (_, _) => OnToolButtonChecked(tool);
        button.Unchecked += (_, _) => OnToolButtonUnchecked(tool);
    }

    public void SelectTool(DrawingTool tool) => _coordinator.SelectTool(tool);

    public void Detach()
    {
        _session.Changed -= SyncFromSession;
        _toolButtons.Clear();
    }

    public bool IsToolSelected(DrawingTool tool) =>
        _toolButtons.TryGetValue(tool, out var button) && button.IsChecked == true;

    public int SelectedButtonCount()
    {
        var count = 0;
        foreach (var button in _toolButtons.Values)
        {
            if (button.IsChecked == true)
            {
                count++;
            }
        }

        return count;
    }

    public void SyncButtonStates()
    {
        _syncing = true;
        try
        {
            foreach (var (tool, button) in _toolButtons)
            {
                button.IsChecked = IsToolVisuallySelected(tool);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private bool IsToolVisuallySelected(DrawingTool tool)
    {
        if (tool == DrawingTool.Text)
        {
            return _coordinator.IsAwaitingTextPlacement || _session.ActiveTool == DrawingTool.Text;
        }

        return tool == _coordinator.PersistentTool && !_coordinator.IsAwaitingTextPlacement;
    }

    private void OnToolButtonChecked(DrawingTool tool)
    {
        if (_syncing)
        {
            return;
        }

        _coordinator.SelectTool(tool);
        SyncButtonStates();
    }

    private void OnToolButtonUnchecked(DrawingTool tool)
    {
        if (_syncing)
        {
            return;
        }

        if (IsToolVisuallySelected(tool))
        {
            SelectTool(tool);
        }
    }

    private void SyncFromSession() => SyncButtonStates();
}
