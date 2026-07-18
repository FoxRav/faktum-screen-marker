using System.Windows;
using Faktum.ScreenMarker.Core.Settings;

namespace Faktum.ScreenMarker.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _original = settings;
        ResultSettings = CloneSettings(settings);
        LanguageCombo.SelectedIndex = settings.LanguageOverride?.StartsWith("fi", StringComparison.OrdinalIgnoreCase) == true ? 1 : 0;
    }

    public AppSettings? ResultSettings { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var languageTag = (LanguageCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
        var settings = ResultSettings ?? CloneSettings(_original);
        settings.LanguageOverride = languageTag;
        ResultSettings = settings;
        DialogResult = true;
        Close();
    }

    private static AppSettings CloneSettings(AppSettings source) =>
        new()
        {
            Version = source.Version,
            PreferredColor = source.PreferredColor,
            PreferredStrokeWidth = source.PreferredStrokeWidth,
            PreferredTextFontSize = source.PreferredTextFontSize,
            ToolbarPlacement = source.ToolbarPlacement,
            LanguageOverride = source.LanguageOverride,
            StartWithWindows = source.StartWithWindows,
        };
}
