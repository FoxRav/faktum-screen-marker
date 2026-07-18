using System.Drawing;
using System.Windows.Forms;

namespace Faktum.ScreenMarker.App.Tray;

public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _icon;

    public void Initialize(ContextMenuStrip menu)
    {
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Faktum Screen Marker",
            ContextMenuStrip = menu,
        };
    }

    public void ShowBalloon(string title, string message)
    {
        _icon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_icon is null)
        {
            return;
        }

        _icon.Visible = false;
        _icon.Dispose();
        _icon = null;
    }
}
