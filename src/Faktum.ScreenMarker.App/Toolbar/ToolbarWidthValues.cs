namespace Faktum.ScreenMarker.App.Toolbar;

public static class ToolbarWidthValues
{
    public static readonly double[] SelectableWidths = [2.0, 4.0, 8.0];

    public const double InvalidFallback = 4.0;

    public static double Normalize(double width)
    {
        foreach (var candidate in SelectableWidths)
        {
            if (Math.Abs(candidate - width) < 0.001)
            {
                return candidate;
            }
        }

        return InvalidFallback;
    }

    public static double NearestSelectable(double width)
    {
        var best = SelectableWidths[0];
        var bestDistance = Math.Abs(width - best);
        foreach (var candidate in SelectableWidths)
        {
            var distance = Math.Abs(width - candidate);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}
