using Faktum.ScreenMarker.Core.Settings;

namespace Faktum.ScreenMarker.App.Toolbar;

public static class ToolbarTextFontSizeValues
{
    public static readonly double[] SelectableSizes = TextFontSizeValues.SelectableSizes;

    public const double Default = TextFontSizeValues.Default;

    public const double InvalidFallback = TextFontSizeValues.InvalidFallback;

    public static double Normalize(double size) => TextFontSizeValues.Normalize(size);

    public static double NearestSelectable(double size) => TextFontSizeValues.NearestSelectable(size);
}
