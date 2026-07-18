using System.Windows;
using System.Windows.Input;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.App.Interaction;

public readonly record struct TextEditorResult(bool Committed, string? Text);

public interface ITextEditorSession
{
    bool IsOpen { get; }

    void FocusEditor();

    void SetFontSize(double fontSize);

    void Cancel();
}

public interface ITextEditorHost
{
    ITextEditorSession Open(
        OverlayWindow overlay,
        Point2D origin,
        StrokeStyle style,
        string monitorDeviceName,
        double fontSize,
        Action<TextEditorResult> onCompleted);
}

internal sealed class WpfTextEditorHost : ITextEditorHost
{
    public ITextEditorSession Open(
        OverlayWindow overlay,
        Point2D origin,
        StrokeStyle style,
        string monitorDeviceName,
        double fontSize,
        Action<TextEditorResult> onCompleted) =>
        TextEditorControl.Open(overlay.TextEditorLayer, origin, style, monitorDeviceName, fontSize, onCompleted);
}
