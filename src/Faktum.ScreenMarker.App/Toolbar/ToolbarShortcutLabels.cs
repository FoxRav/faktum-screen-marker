using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.App.Toolbar;

internal static class ToolbarShortcutLabels
{
    public static string ToolLabel(DrawingTool tool) =>
        tool switch
        {
            DrawingTool.Pen => "Pen (Q)",
            DrawingTool.Line => "Line (W)",
            DrawingTool.Arrow => "Arrow (E)",
            DrawingTool.Rectangle => "Rect (A)",
            DrawingTool.Ellipse => "Ellipse (S)",
            DrawingTool.Text => "Text (Z)",
            DrawingTool.Eraser => "Eraser (X)",
            _ => tool.ToString(),
        };

    public static string ColorLabel(ColorValue color, int digit) =>
        $"{ToolbarControlIds.ColorIdFor(color).Replace("Color.", string.Empty, StringComparison.Ordinal)} ({digit})";

    public static string WidthLabel(double width, int digit) =>
        $"{width.ToString("0", System.Globalization.CultureInfo.InvariantCulture)} ({digit})";
}
