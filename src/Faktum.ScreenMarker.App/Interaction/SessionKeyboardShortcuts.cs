using System.Windows.Input;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Interaction;

namespace Faktum.ScreenMarker.App.Interaction;

internal static class SessionKeyboardShortcuts
{
    private static readonly ColorValue[] DigitColors =
    [
        ColorValue.Red,
        ColorValue.Green,
        ColorValue.Blue,
        ColorValue.Yellow,
        ColorValue.White,
        ColorValue.Black,
    ];

    public static bool TryHandle(ToolbarInteractionCoordinator coordinator, Key key, ModifierKeys modifiers)
    {
        if (coordinator.State is InteractionState.Deactivating or InteractionState.TextEditing)
        {
            return false;
        }

        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
        {
            return false;
        }

        if (TryHandleTool(coordinator, key))
        {
            return true;
        }

        if (TryHandleWidth(coordinator, key))
        {
            return true;
        }

        return TryHandleColor(coordinator, key);
    }

    private static bool TryHandleTool(ToolbarInteractionCoordinator coordinator, Key key)
    {
        var tool = key switch
        {
            Key.Q => DrawingTool.Pen,
            Key.W => DrawingTool.Line,
            Key.E => DrawingTool.Arrow,
            Key.A => DrawingTool.Rectangle,
            Key.S => DrawingTool.Ellipse,
            Key.Z => DrawingTool.Text,
            Key.X => DrawingTool.Eraser,
            _ => (DrawingTool?)null,
        };

        if (tool is null)
        {
            return false;
        }

        coordinator.SelectTool(tool.Value);
        return true;
    }

    private static bool TryHandleWidth(ToolbarInteractionCoordinator coordinator, Key key)
    {
        var width = key switch
        {
            Key.D1 or Key.NumPad1 => 2.0,
            Key.D2 or Key.NumPad2 => 4.0,
            Key.D3 or Key.NumPad3 => 8.0,
            _ => (double?)null,
        };

        if (width is null)
        {
            return false;
        }

        coordinator.SelectWidth(width.Value);
        return true;
    }

    private static bool TryHandleColor(ToolbarInteractionCoordinator coordinator, Key key)
    {
        var index = key switch
        {
            Key.D4 or Key.NumPad4 => 0,
            Key.D5 or Key.NumPad5 => 1,
            Key.D6 or Key.NumPad6 => 2,
            Key.D7 or Key.NumPad7 => 3,
            Key.D8 or Key.NumPad8 => 4,
            Key.D9 or Key.NumPad9 => 5,
            _ => -1,
        };

        if (index < 0)
        {
            return false;
        }

        coordinator.SelectColor(DigitColors[index]);
        return true;
    }
}
