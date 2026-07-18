namespace Faktum.ScreenMarker.Platform.Windows.Diagnostics;

public readonly record struct OverlayInputDiagnosticState(
    nint OverlayHwnd,
    string MonitorDeviceName,
    double ClientWidth,
    double ClientHeight,
    double InputSurfaceWidth,
    double InputSurfaceHeight,
    long ExtendedStyleFlags,
    bool HasTransparentStyle,
    bool HasNoActivateStyle,
    bool InitSucceeded)
{
    public string ToPrivacySafeLine() =>
        $"hwnd=0x{OverlayHwnd:X};monitor={MonitorDeviceName};client={ClientWidth:F0}x{ClientHeight:F0};" +
        $"input={InputSurfaceWidth:F0}x{InputSurfaceHeight:F0};exstyle=0x{ExtendedStyleFlags:X};" +
        $"transparent={HasTransparentStyle};noactivate={HasNoActivateStyle};ok={InitSucceeded}";
}
