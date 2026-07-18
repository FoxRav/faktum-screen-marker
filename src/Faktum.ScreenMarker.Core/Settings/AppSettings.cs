using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.Core.Settings;

public sealed class AppSettings
{
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    public ColorValue PreferredColor { get; set; } = ColorValue.Red;

    public double PreferredStrokeWidth { get; set; } = 3.0;

    public double PreferredTextFontSize { get; set; } = 24.0;

    public ToolbarPlacement? ToolbarPlacement { get; set; }

    public string? LanguageOverride { get; set; }

    public bool StartWithWindows { get; set; }

    public static AppSettings CreateDefault() => new();
}

public readonly record struct ModifierHotkey(bool Control, bool Shift, bool Alt, int VirtualKey)
{
    public static ModifierHotkey Default => new(Control: true, Shift: true, Alt: false, VirtualKey: 0x7B); // F12
}

public readonly record struct ToolbarPlacement(string MonitorDeviceName, double Left, double Top);
