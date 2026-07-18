using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.App.Toolbar;

public static class ToolbarControlIds
{
    public static class Tool
    {
        public const string Pen = "Tool.Pen";
        public const string Line = "Tool.Line";
        public const string Arrow = "Tool.Arrow";
        public const string Rectangle = "Tool.Rectangle";
        public const string Ellipse = "Tool.Ellipse";
        public const string Text = "Tool.Text";
        public const string Eraser = "Tool.Eraser";
    }

    public static class Color
    {
        public const string Red = "Color.Red";
        public const string Green = "Color.Green";
        public const string Blue = "Color.Blue";
        public const string Yellow = "Color.Yellow";
        public const string White = "Color.White";
        public const string Black = "Color.Black";
    }

    public static class Width
    {
        public const string W2 = "Width.2";
        public const string W4 = "Width.4";
        public const string W8 = "Width.8";
    }

    public static class Action
    {
        public const string Undo = "Action.Undo";
        public const string Redo = "Action.Redo";
        public const string Clear = "Action.Clear";
        public const string Close = "Action.Close";
    }

    public static class Text
    {
        public const string FontSize = "Text.FontSize";
    }

    public static string ColorIdFor(ColorValue color) =>
        color switch
        {
            { R: 255, G: 0, B: 0 } => Color.Red,
            { R: 0, G: 180, B: 0 } => Color.Green,
            { R: 0, G: 102, B: 204 } => Color.Blue,
            { R: 255, G: 204, B: 0 } => Color.Yellow,
            { R: 255, G: 255, B: 255 } => Color.White,
            { R: 0, G: 0, B: 0 } => Color.Black,
            _ => $"Color.{color.R}.{color.G}.{color.B}",
        };

    public static string WidthIdFor(double width) =>
        width switch
        {
            2.0 => Width.W2,
            4.0 => Width.W4,
            8.0 => Width.W8,
            _ => Width.W4,
        };
}
