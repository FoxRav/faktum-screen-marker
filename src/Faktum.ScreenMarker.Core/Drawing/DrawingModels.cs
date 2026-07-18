namespace Faktum.ScreenMarker.Core.Drawing;

public enum DrawingTool
{
    Pen,
    Line,
    Arrow,
    Rectangle,
    Ellipse,
    Text,
    Eraser,
}

public readonly record struct ColorValue(byte R, byte G, byte B, byte A = 255)
{
    public static ColorValue Red => new(255, 0, 0);
    public static ColorValue Green => new(0, 180, 0);
    public static ColorValue Blue => new(0, 102, 204);
    public static ColorValue Yellow => new(255, 204, 0);
    public static ColorValue White => new(255, 255, 255);
    public static ColorValue Black => new(0, 0, 0);

    public static IReadOnlyList<ColorValue> DefaultPalette { get; } =
        [Red, Green, Blue, Yellow, White, Black];
}

public readonly record struct StrokeStyle(ColorValue Color, double Width)
{
    public static StrokeStyle DefaultPen => new(ColorValue.Red, 3.0);

    public StrokeStyle WithWidth(double width) => new(Color, width);
}

public readonly record struct Point2D(double X, double Y)
{
    public double DistanceTo(Point2D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public readonly record struct Rect2D(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Contains(Point2D point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    public Rect2D Inflate(double amount) =>
        new(X - amount, Y - amount, Width + (amount * 2), Height + (amount * 2));
}
