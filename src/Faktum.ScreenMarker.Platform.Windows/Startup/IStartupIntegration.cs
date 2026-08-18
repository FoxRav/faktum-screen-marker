namespace Faktum.ScreenMarker.Platform.Windows.Startup;

/// <summary>
/// Manages the per-user (HKCU) Run autostart entry for Faktum Screen Marker.
/// </summary>
/// <remarks>
/// <para>
/// <c>StartWithWindows</c> in settings.json is the single source of truth for the
/// desired autostart state. This component reconciles the HKCU Run entry to match
/// that state: when enabled it writes a well-formed entry targeting the current
/// executable (repairing missing, stale, or malformed values), and when disabled it
/// removes the entry so autostart is never silently re-enabled.
/// </para>
/// </remarks>
public interface IStartupIntegration
{
    /// <summary>
    /// Reconciles the HKCU Run entry with <paramref name="enabled"/> so that the
    /// application starts with Windows exactly when desired, always targeting
    /// <paramref name="executablePath"/> (the current <c>Environment.ProcessPath</c>).
    /// </summary>
    /// <param name="enabled">Desired autostart state, mirroring <c>StartWithWindows</c>.</param>
    /// <param name="executablePath">Current executable path to register when enabled.</param>
    void Apply(bool enabled, string executablePath);
}
