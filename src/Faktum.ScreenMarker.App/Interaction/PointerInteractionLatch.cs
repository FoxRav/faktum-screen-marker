using Faktum.ScreenMarker.Core.Drawing;

namespace Faktum.ScreenMarker.App.Interaction;

internal readonly record struct PointerInteractionLatch(
    DrawingTool Tool,
    StrokeStyle Style,
    string MonitorDeviceName)
{
    public static PointerInteractionLatch FromSession(DrawingSession session, string monitorDeviceName) =>
        new(session.ActiveTool, session.ActiveStyle, monitorDeviceName);
}
