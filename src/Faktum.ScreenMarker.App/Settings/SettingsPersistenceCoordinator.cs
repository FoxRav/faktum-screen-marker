using System.Windows.Threading;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Settings;
using Faktum.ScreenMarker.Platform.Windows.Diagnostics;
using Faktum.ScreenMarker.Platform.Windows.Settings;

namespace Faktum.ScreenMarker.App.Settings;

public sealed class SettingsPersistenceCoordinator : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Action<string, string>? _notifyFailure;
    private readonly DispatcherTimer _debounceTimer;
    private readonly EventHandler _debounceTickHandler;
    private bool _dirty;
    private bool _saveFailureNotified;
    private bool _disposed;

    public SettingsPersistenceCoordinator(AppSettings settings, Action<string, string>? notifyFailure = null)
    {
        _settings = settings;
        _notifyFailure = notifyFailure;
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounceTickHandler = (_, _) =>
        {
            _debounceTimer.Stop();
            Flush();
        };
        _debounceTimer.Tick += _debounceTickHandler;
    }

    public AppSettings Current => _settings;

    public void ApplySettings(AppSettings settings)
    {
        if (_disposed)
        {
            return;
        }

        _settings.Version = settings.Version;
        _settings.PreferredColor = settings.PreferredColor;
        _settings.PreferredStrokeWidth = settings.PreferredStrokeWidth;
        _settings.PreferredTextFontSize = settings.PreferredTextFontSize;
        _settings.ToolbarPlacement = settings.ToolbarPlacement;
        _settings.LanguageOverride = settings.LanguageOverride;
        _settings.StartWithWindows = settings.StartWithWindows;
        MarkDirty();
    }

    public void UpdatePreferredColor(ColorValue color)
    {
        if (_disposed)
        {
            return;
        }

        _settings.PreferredColor = color;
        MarkDirty();
    }

    public void UpdatePreferredStrokeWidth(double width)
    {
        if (_disposed)
        {
            return;
        }

        _settings.PreferredStrokeWidth = width;
        MarkDirty();
    }

    public void UpdatePreferredTextFontSize(double fontSize)
    {
        if (_disposed)
        {
            return;
        }

        _settings.PreferredTextFontSize = fontSize;
        MarkDirty();
    }

    public void UpdateToolbarPlacement(ToolbarPlacement placement)
    {
        if (_disposed)
        {
            return;
        }

        _settings.ToolbarPlacement = placement;
        MarkDirty();
    }

    public void UpdateStartWithWindows(bool enabled)
    {
        if (_disposed)
        {
            return;
        }

        _settings.StartWithWindows = enabled;
        MarkDirty();
    }

    public void Flush()
    {
        if (_disposed)
        {
            return;
        }

        _debounceTimer.Stop();
        if (!_dirty)
        {
            return;
        }

        try
        {
            SettingsService.Save(_settings);
            _dirty = false;
            _saveFailureNotified = false;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Settings", ex.GetType().Name);
            if (!_saveFailureNotified)
            {
                _saveFailureNotified = true;
                _notifyFailure?.Invoke("SettingsSaveFailedTitle", "SettingsSaveFailedMessage");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _debounceTimer.Stop();
        Flush();
        _debounceTimer.Tick -= _debounceTickHandler;
        _disposed = true;
    }

    private void MarkDirty()
    {
        if (_disposed)
        {
            return;
        }

        _dirty = true;
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }
}
