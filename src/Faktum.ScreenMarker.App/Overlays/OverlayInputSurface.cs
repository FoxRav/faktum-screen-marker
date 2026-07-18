using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Faktum.ScreenMarker.App.Drawing;
using WpfColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace Faktum.ScreenMarker.App.Overlays;

/// <summary>
/// Full-window hit-test surface with a nearly invisible background so every pixel
/// participates in WPF hit-testing while remaining visually transparent.
/// </summary>
public sealed class OverlayInputSurface : Grid
{
    public const byte InputCaptureAlpha = 1;

    public DrawingSurface DrawingSurface { get; }

    public OverlayInputSurface(DrawingSurface drawingSurface)
    {
        DrawingSurface = drawingSurface;
        IsHitTestVisible = true;
        HorizontalAlignment = WpfHorizontalAlignment.Stretch;
        VerticalAlignment = WpfVerticalAlignment.Stretch;
        Background = new SolidColorBrush(WpfColor.FromArgb(InputCaptureAlpha, 0, 0, 0));

        drawingSurface.HorizontalAlignment = WpfHorizontalAlignment.Stretch;
        drawingSurface.VerticalAlignment = WpfVerticalAlignment.Stretch;
        drawingSurface.IsHitTestVisible = false;

        Children.Add(drawingSurface);
    }
}
