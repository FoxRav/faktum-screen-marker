namespace Faktum.ScreenMarker.Core.Settings;

public static class TextFontSizeValues
{
    public static readonly double[] SelectableSizes = [16.0, 24.0, 32.0, 48.0, 64.0, 96.0];

    public const double Default = 24.0;

    public const double InvalidFallback = 24.0;

    public static double Normalize(double size)
    {
        foreach (var candidate in SelectableSizes)
        {
            if (Math.Abs(candidate - size) < 0.001)
            {
                return candidate;
            }
        }

        return InvalidFallback;
    }

    public static double NearestSelectable(double size)
    {
        var best = SelectableSizes[0];
        var bestDistance = Math.Abs(size - best);
        foreach (var candidate in SelectableSizes)
        {
            var distance = Math.Abs(size - candidate);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static double ValidateOnLoad(double size)
    {
        var normalized = Normalize(size);
        if (Math.Abs(normalized - size) < 0.001)
        {
            return normalized;
        }

        return NearestSelectable(size);
    }
}
