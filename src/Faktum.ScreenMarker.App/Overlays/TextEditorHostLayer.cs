using System.Windows;
using System.Windows.Controls;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace Faktum.ScreenMarker.App.Overlays;

internal sealed class TextEditorHostLayer : Canvas
{
    public TextEditorHostLayer()
    {
        System.Windows.Controls.Panel.SetZIndex(this, OverlayLayerZIndex.TextEditorHost);
        IsHitTestVisible = false;
        HorizontalAlignment = WpfHorizontalAlignment.Stretch;
        VerticalAlignment = WpfVerticalAlignment.Stretch;
    }

    internal void AttachEditor(UIElement editor, double left, double top)
    {
        Children.Clear();
        IsHitTestVisible = true;
        SetLeft(editor, left);
        SetTop(editor, top);
        Children.Add(editor);
    }

    internal void RemoveEditor()
    {
        Children.Clear();
        IsHitTestVisible = false;
    }
}
