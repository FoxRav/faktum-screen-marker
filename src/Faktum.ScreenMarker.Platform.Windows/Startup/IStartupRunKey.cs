namespace Faktum.ScreenMarker.Platform.Windows.Startup;

/// <summary>
/// Minimal surface over the HKCU Run registry key, abstracted so the startup
/// reconciliation logic can be unit tested without touching the real per-user
/// registry hive.
/// </summary>
internal interface IStartupRunKey
{
    string? GetValue(string name);

    void SetValue(string name, string value);

    void DeleteValue(string name, bool throwOnMissingValue);
}
