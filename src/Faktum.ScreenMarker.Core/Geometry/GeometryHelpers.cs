using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.Core.Geometry;

public static class GeometryHelpers
{
    public const double JitterThresholdDips = 0.75;

    public static Rect2D NormalizeRect(Point2D start, Point2D end, bool constrainSquare = false)
    {
        var x1 = start.X;
        var y1 = start.Y;
        var x2 = end.X;
        var y2 = end.Y;

        if (constrainSquare)
        {
            var size = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
            x2 = x1 + (Math.Sign(x2 - x1) * size);
            y2 = y1 + (Math.Sign(y2 - y1) * size);
        }

        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var width = Math.Abs(x2 - x1);
        var height = Math.Abs(y2 - y1);
        return new Rect2D(left, top, width, height);
    }

    public static (Point2D Start, Point2D End) ConstrainLine(Point2D start, Point2D end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var angle = Math.Atan2(dy, dx);
        const double step = Math.PI / 4.0;
        var snapped = Math.Round(angle / step) * step;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var constrainedEnd = new Point2D(
            start.X + (Math.Cos(snapped) * length),
            start.Y + (Math.Sin(snapped) * length));
        return (start, constrainedEnd);
    }

    public static bool ShouldIgnoreJitter(Point2D previous, Point2D current) =>
        previous.DistanceTo(current) < JitterThresholdDips;

    public static IReadOnlyList<Point2D> BuildArrowHead(Point2D start, Point2D end, double headLength, double headAngleRadians)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var left = new Point2D(
            end.X - (headLength * Math.Cos(angle - headAngleRadians)),
            end.Y - (headLength * Math.Sin(angle - headAngleRadians)));
        var right = new Point2D(
            end.X - (headLength * Math.Cos(angle + headAngleRadians)),
            end.Y - (headLength * Math.Sin(angle + headAngleRadians)));
        return [end, left, end, right];
    }
}
