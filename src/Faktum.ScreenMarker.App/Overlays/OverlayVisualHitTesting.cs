using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Faktum.ScreenMarker.App.Interaction;
using Faktum.ScreenMarker.App.Toolbar;
using WpfButton = System.Windows.Controls.Button;
using WpfPoint = System.Windows.Point;

namespace Faktum.ScreenMarker.App.Overlays;

internal static class OverlayVisualHitTesting
{
    public static IReadOnlyList<string> AllToolbarControlAutomationIds { get; } =
    [
        ToolbarControlIds.Tool.Pen,
        ToolbarControlIds.Tool.Line,
        ToolbarControlIds.Tool.Arrow,
        ToolbarControlIds.Tool.Rectangle,
        ToolbarControlIds.Tool.Ellipse,
        ToolbarControlIds.Tool.Text,
        ToolbarControlIds.Tool.Eraser,
        ToolbarControlIds.Text.FontSize,
        ToolbarControlIds.Color.Red,
        ToolbarControlIds.Color.Green,
        ToolbarControlIds.Color.Blue,
        ToolbarControlIds.Color.Yellow,
        ToolbarControlIds.Color.White,
        ToolbarControlIds.Color.Black,
        ToolbarControlIds.Width.W2,
        ToolbarControlIds.Width.W4,
        ToolbarControlIds.Width.W8,
        ToolbarControlIds.Action.Undo,
        ToolbarControlIds.Action.Redo,
        ToolbarControlIds.Action.Clear,
        ToolbarControlIds.Action.Close,
    ];

    public static bool EmptyAreaHitsInputSurface(OverlayInputSurface inputSurface, WpfPoint pointInSurface)
    {
        var hit = VisualTreeHelper.HitTest(inputSurface, pointInSurface);
        if (hit?.VisualHit is not DependencyObject current)
        {
            return false;
        }

        while (current is not null)
        {
            if (current is OverlayInputSurface)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    public static bool HitTestControlCenter(FrameworkElement root, FrameworkElement expectedControl, out string? failedAutomationId)
    {
        expectedControl.UpdateLayout();
        root.UpdateLayout();

        if (expectedControl.ActualWidth <= 0 || expectedControl.ActualHeight <= 0)
        {
            failedAutomationId = AutomationProperties.GetAutomationId(expectedControl);
            return false;
        }

        var centerInRoot = ResolveControlCenterInRoot(root, expectedControl);
        var hit = VisualTreeHelper.HitTest(root, centerInRoot);
        if (hit?.VisualHit is not DependencyObject hitObject || !IsSameOrAncestorOf(hitObject, expectedControl))
        {
            failedAutomationId = AutomationProperties.GetAutomationId(expectedControl);
            return false;
        }

        failedAutomationId = null;
        return true;
    }

    public static bool VerifyToolbarControls(OverlayWindow overlay, ToolbarControl toolbar, out string? failedAutomationId)
    {
        foreach (var automationId in AllToolbarControlAutomationIds)
        {
            var control = toolbar.FindControlByAutomationId(automationId);
            if (control is null)
            {
                failedAutomationId = automationId;
                return false;
            }

            if (!HitTestControlCenter(overlay.OverlayRoot, control, out failedAutomationId))
            {
                return false;
            }
        }

        failedAutomationId = null;
        return true;
    }

    public static bool VerifyEmptyAreaHitsInputSurface(OverlayWindow overlay, out string? failedAutomationId)
    {
        var point = ResolveEmptyInputSamplePoint(overlay);
        var hit = VisualTreeHelper.HitTest(overlay.OverlayRoot, point);
        if (hit?.VisualHit is not DependencyObject hitObject || !IsSameOrAncestorOf(hitObject, overlay.InputSurface))
        {
            failedAutomationId = "OverlayInputSurface.EmptyArea";
            return false;
        }

        failedAutomationId = null;
        return true;
    }

    public static bool VerifyTextEditorHits(OverlayWindow overlay, out string? failedAutomationId)
    {
        var editor = overlay.TextEditorLayer.Children.OfType<TextEditorControl>().FirstOrDefault();
        if (editor?.Content is not System.Windows.Controls.TextBox textBox)
        {
            failedAutomationId = "TextEditor.TextBox";
            return false;
        }

        if (!HitTestControlCenter(overlay.OverlayRoot, textBox, out failedAutomationId))
        {
            return false;
        }

        failedAutomationId = null;
        return true;
    }

    public static bool VerifyToolbarAndInputSurface(OverlayWindow overlay, ToolbarControl toolbar, out string? failedAutomationId)
    {
        if (!VerifyToolbarControls(overlay, toolbar, out failedAutomationId))
        {
            return false;
        }

        return VerifyEmptyAreaHitsInputSurface(overlay, out failedAutomationId);
    }

    public static WpfPoint ResolveControlCenterInRoot(FrameworkElement root, FrameworkElement control)
    {
        control.UpdateLayout();
        root.UpdateLayout();
        var centerInControl = new WpfPoint(control.ActualWidth / 2, control.ActualHeight / 2);
        return control.TransformToAncestor(root).Transform(centerInControl);
    }

    public static WpfPoint ResolveEmptyInputSamplePoint(OverlayWindow overlay)
    {
        overlay.UpdateLayout();
        var width = overlay.ActualWidth > 0 ? overlay.ActualWidth : overlay.Width;
        var height = overlay.ActualHeight > 0 ? overlay.ActualHeight : overlay.Height;
        var toolbarHost = overlay.ToolbarHost;
        toolbarHost.UpdateLayout();
        var sample = new WpfPoint(Math.Max(8, width - 16), Math.Max(8, height - 16));
        if (toolbarHost.ActualWidth > 0 && toolbarHost.ActualHeight > 0)
        {
            var toolbarRect = new Rect(
                toolbarHost.Margin.Left,
                toolbarHost.Margin.Top,
                toolbarHost.ActualWidth,
                toolbarHost.ActualHeight);
            if (toolbarRect.Contains(sample))
            {
                sample = new WpfPoint(8, Math.Max(8, height - 16));
            }
        }

        return sample;
    }

    private static bool IsSameOrAncestorOf(DependencyObject? hit, DependencyObject expected)
    {
        var current = hit;
        while (current is not null)
        {
            if (ReferenceEquals(current, expected))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}

internal static class ToolbarRoutedInput
{
    public static void InvokeAtCenter(FrameworkElement control, FrameworkElement root)
    {
        control.UpdateLayout();
        root.UpdateLayout();
        if (!OverlayVisualHitTesting.HitTestControlCenter(root, control, out var failedId))
        {
            throw new InvalidOperationException($"Hit test failed before routed input for '{failedId}'.");
        }

        if (control is ToggleButton toggle)
        {
            if (toggle.IsChecked != true)
            {
                toggle.IsChecked = true;
            }

            return;
        }

        if (control is WpfButton button)
        {
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = button,
            });
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent,
                Source = button,
            });
            button.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent, button));
            return;
        }

        throw new InvalidOperationException(
            $"Control '{AutomationProperties.GetAutomationId(control)}' does not support routed input.");
    }
}
