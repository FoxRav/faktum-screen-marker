using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.Core.Geometry;

public static class HitTesting
{
    public const double DefaultToleranceDips = 6.0;

    public static bool HitTest(DrawingObject obj, Point2D point, double tolerance = DefaultToleranceDips)
    {
        return obj switch
        {
            FreehandStroke stroke => HitTestPolyline(stroke.Points, point, Math.Max(tolerance, stroke.Style.Width / 2.0)),
            LineAnnotation line => DistancePointToSegment(point, line.Start, line.End) <= Math.Max(tolerance, line.Style.Width / 2.0),
            ArrowAnnotation arrow => DistancePointToSegment(point, arrow.Start, arrow.End) <= Math.Max(tolerance, arrow.Style.Width / 2.0),
            RectangleAnnotation rect => HitTestRectangle(rect.Bounds, point, Math.Max(tolerance, rect.Style.Width / 2.0)),
            EllipseAnnotation ellipse => HitTestEllipse(ellipse.Bounds, point, Math.Max(tolerance, ellipse.Style.Width / 2.0)),
            TextAnnotation text => HitTestText(text, point, tolerance),
            _ => false,
        };
    }

    public static int? FindTopmostHit(IReadOnlyList<DrawingObject> objects, Point2D point, double tolerance = DefaultToleranceDips)
    {
        for (var i = objects.Count - 1; i >= 0; i--)
        {
            if (HitTest(objects[i], point, tolerance))
            {
                return objects[i].Id;
            }
        }

        return null;
    }

    private static bool HitTestPolyline(IReadOnlyList<Point2D> points, Point2D point, double tolerance)
    {
        if (points.Count < 2)
        {
            return points.Count == 1 && points[0].DistanceTo(point) <= tolerance;
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (DistancePointToSegment(point, points[i - 1], points[i]) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HitTestRectangle(Rect2D bounds, Point2D point, double tolerance)
    {
        var inflated = bounds.Inflate(tolerance);
        if (!inflated.Contains(point))
        {
            return false;
        }

        var inner = bounds.Inflate(-tolerance);
        if (inner.Width <= 0 || inner.Height <= 0)
        {
            return true;
        }

        return !inner.Contains(point);
    }

    private static bool HitTestEllipse(Rect2D bounds, Point2D point, double tolerance)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var cx = bounds.X + (bounds.Width / 2.0);
        var cy = bounds.Y + (bounds.Height / 2.0);
        var rx = (bounds.Width / 2.0) + tolerance;
        var ry = (bounds.Height / 2.0) + tolerance;
        var dx = point.X - cx;
        var dy = point.Y - cy;
        var value = ((dx * dx) / (rx * rx)) + ((dy * dy) / (ry * ry));
        if (value > 1.0)
        {
            return false;
        }

        var innerRx = Math.Max(0.0, (bounds.Width / 2.0) - tolerance);
        var innerRy = Math.Max(0.0, (bounds.Height / 2.0) - tolerance);
        if (innerRx <= 0 || innerRy <= 0)
        {
            return true;
        }

        var innerValue = ((dx * dx) / (innerRx * innerRx)) + ((dy * dy) / (innerRy * innerRy));
        return innerValue > 1.0;
    }

    private static bool HitTestText(TextAnnotation text, Point2D point, double tolerance)
    {
        var width = Math.Max(24.0, text.Text.Length * (text.FontSize * 0.55));
        var height = text.FontSize * 1.4;
        var bounds = new Rect2D(text.Origin.X, text.Origin.Y, width, height).Inflate(tolerance);
        return bounds.Contains(point);
    }

    private static double DistancePointToSegment(Point2D point, Point2D a, Point2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        if (dx == 0 && dy == 0)
        {
            return point.DistanceTo(a);
        }

        var t = (((point.X - a.X) * dx) + ((point.Y - a.Y) * dy)) / ((dx * dx) + (dy * dy));
        t = Math.Clamp(t, 0.0, 1.0);
        var projection = new Point2D(a.X + (t * dx), a.Y + (t * dy));
        return point.DistanceTo(projection);
    }
}
