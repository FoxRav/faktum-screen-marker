namespace Faktum.ScreenMarker.Core.Drawing;

public abstract record DrawingObject(int Id, string MonitorDeviceName, StrokeStyle Style);

public sealed record FreehandStroke(int Id, string MonitorDeviceName, StrokeStyle Style, IReadOnlyList<Point2D> Points)
    : DrawingObject(Id, MonitorDeviceName, Style);

public sealed record LineAnnotation(int Id, string MonitorDeviceName, StrokeStyle Style, Point2D Start, Point2D End)
    : DrawingObject(Id, MonitorDeviceName, Style);

public sealed record ArrowAnnotation(int Id, string MonitorDeviceName, StrokeStyle Style, Point2D Start, Point2D End)
    : DrawingObject(Id, MonitorDeviceName, Style);

public sealed record RectangleAnnotation(int Id, string MonitorDeviceName, StrokeStyle Style, Rect2D Bounds)
    : DrawingObject(Id, MonitorDeviceName, Style);

public sealed record EllipseAnnotation(int Id, string MonitorDeviceName, StrokeStyle Style, Rect2D Bounds)
    : DrawingObject(Id, MonitorDeviceName, Style);

public sealed record TextAnnotation(
    int Id,
    string MonitorDeviceName,
    StrokeStyle Style,
    Point2D Origin,
    string Text,
    double FontSize) : DrawingObject(Id, MonitorDeviceName, Style);

public static class DrawingValidation
{
    public const int MaxTextLength = 1000;
    public const double MinStrokeWidth = 0.5;
    public const double MaxStrokeWidth = 48.0;

    public static bool IsValidPoint(Point2D point) =>
        IsFinite(point.X) && IsFinite(point.Y);

    public static bool IsValidStrokeWidth(double width) =>
        IsFinite(width) && width >= MinStrokeWidth && width <= MaxStrokeWidth;

    public static bool IsValidRect(Rect2D rect) =>
        IsFinite(rect.X) && IsFinite(rect.Y) && IsFinite(rect.Width) && IsFinite(rect.Height) &&
        rect.Width >= 0 && rect.Height >= 0;

    public static bool IsValidText(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= MaxTextLength;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
