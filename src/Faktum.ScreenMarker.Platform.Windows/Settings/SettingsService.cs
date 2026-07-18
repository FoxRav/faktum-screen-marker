using System.Text.Json;
using System.Text.Json.Serialization;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;

namespace Faktum.ScreenMarker.Platform.Windows.Settings;

public interface ISettingsStore
{
    string SettingsDirectory { get; }

    string SettingsFilePath { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}

public sealed class JsonFileSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public JsonFileSettingsStore(string settingsDirectory)
    {
        if (string.IsNullOrWhiteSpace(settingsDirectory))
        {
            throw new ArgumentException("Settings directory is required.", nameof(settingsDirectory));
        }

        SettingsDirectory = settingsDirectory;
        SettingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public string SettingsDirectory { get; }

    public string SettingsFilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return AppSettings.CreateDefault();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json, JsonOptions);
            return dto?.ToModel() ?? AppSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return AppSettings.CreateDefault();
        }
        catch (IOException)
        {
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var tempPath = SettingsFilePath + ".tmp";
        var dto = SettingsDto.FromModel(settings);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsFilePath, overwrite: true);
    }

    private sealed class SettingsDto
    {
        public int Version { get; set; } = AppSettings.CurrentVersion;

        public ModifierHotkeyDto? FallbackHotkey { get; set; }

        public ColorDto PreferredColor { get; set; } = ColorDto.From(ColorValue.Red);

        public double PreferredStrokeWidth { get; set; } = 3.0;

        public double PreferredTextFontSize { get; set; } = TextFontSizeValues.Default;

        public ToolbarPlacementDto? ToolbarPlacement { get; set; }

        public string? LanguageOverride { get; set; }

        public bool StartWithWindows { get; set; }

        public static SettingsDto FromModel(AppSettings settings) =>
            new()
            {
                Version = settings.Version,
                PreferredColor = ColorDto.From(settings.PreferredColor),
                PreferredStrokeWidth = settings.PreferredStrokeWidth,
                PreferredTextFontSize = settings.PreferredTextFontSize,
                ToolbarPlacement = settings.ToolbarPlacement is null
                    ? null
                    : ToolbarPlacementDto.From(settings.ToolbarPlacement.Value),
                LanguageOverride = settings.LanguageOverride,
                StartWithWindows = settings.StartWithWindows,
            };

        public AppSettings ToModel()
        {
            var strokeWidth = PreferredStrokeWidth;
            if (!DrawingValidation.IsValidStrokeWidth(strokeWidth))
            {
                strokeWidth = 3.0;
            }

            var textFontSize = TextFontSizeValues.ValidateOnLoad(PreferredTextFontSize);

            _ = FallbackHotkey;

            return new AppSettings
            {
                Version = Math.Max(Version, AppSettings.CurrentVersion),
                PreferredColor = PreferredColor.ToModel(),
                PreferredStrokeWidth = strokeWidth,
                PreferredTextFontSize = textFontSize,
                ToolbarPlacement = ToolbarPlacement?.ToModel(),
                LanguageOverride = LanguageOverride,
                StartWithWindows = StartWithWindows,
            };
        }
    }

    private sealed class ModifierHotkeyDto
    {
        public bool Control { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public int VirtualKey { get; set; }

        public static ModifierHotkeyDto From(ModifierHotkey hotkey) =>
            new()
            {
                Control = hotkey.Control,
                Shift = hotkey.Shift,
                Alt = hotkey.Alt,
                VirtualKey = hotkey.VirtualKey,
            };

        public ModifierHotkey ToModel() => new(Control, Shift, Alt, VirtualKey);
    }

    private sealed class ColorDto
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public byte A { get; set; } = 255;

        public static ColorDto From(ColorValue color) =>
            new() { R = color.R, G = color.G, B = color.B, A = color.A };

        public ColorValue ToModel() => new(R, G, B, A);
    }

    private sealed class ToolbarPlacementDto
    {
        public string MonitorDeviceName { get; set; } = string.Empty;
        public double Left { get; set; }
        public double Top { get; set; }

        public static ToolbarPlacementDto From(ToolbarPlacement placement) =>
            new()
            {
                MonitorDeviceName = placement.MonitorDeviceName,
                Left = placement.Left,
                Top = placement.Top,
            };

        public ToolbarPlacement ToModel() => new(MonitorDeviceName, Left, Top);
    }
}

public static class SettingsPaths
{
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FaktumAI", "ScreenMarker");

    public static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");
}

public static class SettingsService
{
    private static ISettingsStore? _overrideStore;

    public static ISettingsStore Store =>
        _overrideStore ??= new JsonFileSettingsStore(SettingsPaths.SettingsDirectory);

    public static void UseStoreForTesting(ISettingsStore store) => _overrideStore = store;

    public static void ResetStoreForTesting() => _overrideStore = null;

    public static AppSettings Load() => Store.Load();

    public static void Save(AppSettings settings) => Store.Save(settings);
}
