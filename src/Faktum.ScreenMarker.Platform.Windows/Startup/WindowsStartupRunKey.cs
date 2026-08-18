using Microsoft.Win32;

namespace Faktum.ScreenMarker.Platform.Windows.Startup;

/// <summary>
/// Real HKCU Run-key implementation backed by the per-user registry hive.
/// </summary>
internal sealed class WindowsStartupRunKey : IStartupRunKey
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static readonly WindowsStartupRunKey Current = new();

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = OpenWritable();
        key?.SetValue(name, value);
    }

    public void DeleteValue(string name, bool throwOnMissingValue)
    {
        using var key = OpenWritable();
        if (key is not null)
        {
            key.DeleteValue(name, throwOnMissingValue);
        }
    }

    private static RegistryKey? OpenWritable() =>
        Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
}
