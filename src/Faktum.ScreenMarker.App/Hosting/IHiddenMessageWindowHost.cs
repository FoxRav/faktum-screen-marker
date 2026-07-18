namespace Faktum.ScreenMarker.App.Hosting;

public interface IHiddenMessageWindowHost
{
    nint Handle { get; }

    bool HasValidHandle { get; }

    event Action? HandleReady;

    event Action<int>? HotkeyPressed;

    event Action? InputLanguageChanged;

    void EnsureHandleCreated();
}
