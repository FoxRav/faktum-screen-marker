namespace Faktum.ScreenMarker.Core.Drawing;

public static class FreehandInputValidation
{
    public const int MaxLivePoints = 32_768;
    public const int MaxPreparedPoints = 16_384;
    public const double ConsecutiveDuplicateEpsilon = 1e-6;

    public static bool TryPrepareForCommit(IReadOnlyList<Point2D> raw, out Point2D[] prepared)
    {
        prepared = Array.Empty<Point2D>();
        if (raw.Count == 0)
        {
            return false;
        }

        var working = new List<Point2D>(raw.Count);
        foreach (var point in raw)
        {
            if (!DrawingValidation.IsValidPoint(point))
            {
                return false;
            }

            if (working.Count > 0 && AreNearDuplicates(working[^1], point))
            {
                continue;
            }

            working.Add(point);
            if (working.Count > MaxPreparedPoints)
            {
                working = UniformSubsample(working, MaxPreparedPoints);
            }
        }

        if (working.Count < 2)
        {
            return false;
        }

        prepared = working.ToArray();
        return true;
    }

    public static bool ShouldAcceptLivePoint(IReadOnlyList<Point2D> current, Point2D candidate)
    {
        if (!DrawingValidation.IsValidPoint(candidate))
        {
            return false;
        }

        if (current.Count >= MaxLivePoints)
        {
            return false;
        }

        return current.Count == 0 || !AreNearDuplicates(current[^1], candidate);
    }

    private static bool AreNearDuplicates(Point2D left, Point2D right) =>
        Math.Abs(left.X - right.X) <= ConsecutiveDuplicateEpsilon &&
        Math.Abs(left.Y - right.Y) <= ConsecutiveDuplicateEpsilon;

    private static List<Point2D> UniformSubsample(List<Point2D> points, int maxCount)
    {
        if (points.Count <= maxCount)
        {
            return points.ToList();
        }

        var result = new List<Point2D>(maxCount);
        var lastIndex = points.Count - 1;
        for (var i = 0; i < maxCount; i++)
        {
            var sourceIndex = (int)Math.Round(i * lastIndex / (double)(maxCount - 1));
            var point = points[sourceIndex];
            if (result.Count == 0 || !AreNearDuplicates(result[^1], point))
            {
                result.Add(point);
            }
        }

        if (result.Count < 2)
        {
            result.Add(points[lastIndex]);
        }

        return result;
    }
}
