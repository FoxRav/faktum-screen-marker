namespace Faktum.ScreenMarker.Platform.Windows.Startup;

/// <summary>
/// Real HKCU Run-key implementation of <see cref="IStartupIntegration"/>.
/// </summary>
public sealed class StartupIntegration : IStartupIntegration
{
    internal const string ValueName = "FaktumScreenMarker";

    private readonly IStartupRunKey _runKey;

    public StartupIntegration() : this(null)
    {
    }

    internal StartupIntegration(IStartupRunKey? runKey)
    {
        _runKey = runKey ?? WindowsStartupRunKey.Current;
    }

    public void Apply(bool enabled, string executablePath)
    {
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return;
            }

            var canonical = $"\"{executablePath}\"";
            if (!string.Equals(_runKey.GetValue(ValueName), canonical, StringComparison.OrdinalIgnoreCase))
            {
                _runKey.SetValue(ValueName, canonical);
            }
        }
        else
        {
            _runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
