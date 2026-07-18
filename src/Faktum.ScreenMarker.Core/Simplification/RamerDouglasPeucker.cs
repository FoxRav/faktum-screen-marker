using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.Core.Simplification;

public static class RamerDouglasPeucker
{
    public const double DefaultToleranceDips = 1.5;

    public static IReadOnlyList<Point2D> Simplify(IReadOnlyList<Point2D> points, double tolerance = DefaultToleranceDips)
    {
        ValidateTolerance(tolerance);

        var count = points.Count;
        if (count == 0)
        {
            return Array.Empty<Point2D>();
        }

        if (count <= 2)
        {
            return points.ToArray();
        }

        var keep = new bool[count];
        keep[0] = true;
        keep[count - 1] = true;

        var stack = new Stack<(int Start, int End)>();
        stack.Push((0, count - 1));

        while (stack.Count > 0)
        {
            var (start, end) = stack.Pop();
            if (end <= start + 1)
            {
                continue;
            }

            var maxDistance = 0.0;
            var index = start;
            for (var i = start + 1; i < end; i++)
            {
                var distance = PerpendicularDistance(points[i], points[start], points[end]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    index = i;
                }
            }

            if (maxDistance > tolerance)
            {
                keep[index] = true;
                stack.Push((index, end));
                stack.Push((start, index));
            }
        }

        var result = new List<Point2D>(count);
        for (var i = 0; i < count; i++)
        {
            if (keep[i])
            {
                result.Add(points[i]);
            }
        }

        return result;
    }

    internal static void ValidateTolerance(double tolerance)
    {
        if (double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be a finite non-negative value.");
        }
    }

    private static double PerpendicularDistance(Point2D point, Point2D lineStart, Point2D lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        if (dx == 0 && dy == 0)
        {
            return point.DistanceTo(lineStart);
        }

        var numerator = Math.Abs((dy * point.X) - (dx * point.Y) + (lineEnd.X * lineStart.Y) - (lineEnd.Y * lineStart.X));
        var denominator = Math.Sqrt((dx * dx) + (dy * dy));
        return numerator / denominator;
    }
}
