using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Faktum.ScreenMarker.App.Overlays;
using Faktum.ScreenMarker.Core.Drawing;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace Faktum.ScreenMarker.App.Interaction;

internal sealed class TextEditorControl : System.Windows.Controls.UserControl, ITextEditorSession
{
    private readonly WpfTextBox _textBox;
    private readonly Action<TextEditorResult> _onCompleted;
    private readonly TextEditorHostLayer _hostLayer;
    private bool _completed;

    private TextEditorControl(
        TextEditorHostLayer hostLayer,
        Point2D origin,
        double fontSize,
        Action<TextEditorResult> onCompleted)
    {
        _hostLayer = hostLayer;
        _onCompleted = onCompleted;

        Width = 320;
        Height = 120;
        Background = System.Windows.Media.Brushes.White;
        Focusable = true;

        _textBox = new WpfTextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxLength = DrawingValidation.MaxTextLength,
            Margin = new Thickness(8),
            FontSize = fontSize,
        };
        AutomationProperties.SetAutomationId(_textBox, "TextEditor.TextBox");
        Content = _textBox;

        Loaded += (_, _) =>
        {
            _textBox.Focus();
            Keyboard.Focus(_textBox);
            _textBox.CaretIndex = _textBox.Text.Length;
        };

        KeyDown += OnKeyDown;
        _hostLayer.AttachEditor(this, origin.X + 8, origin.Y);
    }

    public bool IsOpen => !_completed && _hostLayer.Children.Contains(this);

    public void FocusEditor()
    {
        _textBox.Focus();
        Keyboard.Focus(_textBox);
    }

    public void SetFontSize(double fontSize) => _textBox.FontSize = fontSize;

    public void Cancel() => Complete(new TextEditorResult(false, null));

    internal static ITextEditorSession Open(
        TextEditorHostLayer hostLayer,
        Point2D origin,
        StrokeStyle style,
        string monitorDeviceName,
        double fontSize,
        Action<TextEditorResult> onCompleted)
    {
        _ = style;
        _ = monitorDeviceName;
        hostLayer.RemoveEditor();
        return new TextEditorControl(hostLayer, origin, fontSize, onCompleted);
    }

    private void OnKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Complete(new TextEditorResult(false, null));
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            var text = _textBox.Text;
            if (!DrawingValidation.IsValidText(text))
            {
                Complete(new TextEditorResult(false, null));
                return;
            }

            Complete(new TextEditorResult(true, text.Trim()));
        }
    }

    private void Complete(TextEditorResult result)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _hostLayer.RemoveEditor();
        _onCompleted(result);
    }
}
