using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Faktum.ScreenMarker.Core.Drawing;
using Faktum.ScreenMarker.Core.Geometry;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace Faktum.ScreenMarker.App.Drawing;

public sealed class DrawingRenderer
{
    public DrawingVisual CommittedVisual { get; } = new();
    public DrawingVisual PreviewVisual { get; } = new();

    internal double LastRenderedPixelsPerDip { get; private set; }

    public void Render(IReadOnlyList<DrawingObject> committed, DrawingObject? preview, double pixelsPerDip)
    {
        LastRenderedPixelsPerDip = pixelsPerDip;
        RenderVisual(CommittedVisual, committed, pixelsPerDip);
        RenderVisual(PreviewVisual, preview is null ? Array.Empty<DrawingObject>() : [preview], pixelsPerDip);
    }

    private static void RenderVisual(DrawingVisual visual, IReadOnlyList<DrawingObject> objects, double pixelsPerDip)
    {
        using var context = visual.RenderOpen();
        context.DrawRectangle(WpfBrushes.Transparent, null, new Rect(0, 0, 10000, 10000));
        foreach (var obj in objects)
        {
            DrawObject(context, obj, pixelsPerDip);
        }
    }

    private static void DrawObject(DrawingContext context, DrawingObject obj, double pixelsPerDip)
    {
        var pen = CreatePen(obj.Style);
        switch (obj)
        {
            case FreehandStroke stroke:
                DrawPolyline(context, pen, stroke.Points);
                break;
            case LineAnnotation line:
                context.DrawLine(pen, ToPoint(line.Start), ToPoint(line.End));
                break;
            case ArrowAnnotation arrow:
                context.DrawLine(pen, ToPoint(arrow.Start), ToPoint(arrow.End));
                var head = GeometryHelpers.BuildArrowHead(arrow.Start, arrow.End, Math.Max(8.0, arrow.Style.Width * 3.0), Math.PI / 6.0);
                DrawPolyline(context, pen, head);
                break;
            case RectangleAnnotation rect:
                context.DrawRectangle(null, pen, ToRect(rect.Bounds));
                break;
            case EllipseAnnotation ellipse:
            {
                var bounds = ToRect(ellipse.Bounds);
                context.DrawEllipse(
                    null,
                    pen,
                    new WpfPoint(bounds.X + (bounds.Width / 2.0), bounds.Y + (bounds.Height / 2.0)),
                    bounds.Width / 2.0,
                    bounds.Height / 2.0);
                break;
            }
            case TextAnnotation text:
                var formatted = new FormattedText(
                    text.Text,
                    CultureInfo.CurrentUICulture,
                    WpfFlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    text.FontSize,
                    CreateBrush(text.Style),
                    pixelsPerDip);
                context.DrawText(formatted, ToPoint(text.Origin));
                break;
        }
    }

    private static void DrawPolyline(DrawingContext context, WpfPen pen, IReadOnlyList<Point2D> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        for (var i = 1; i < points.Count; i++)
        {
            context.DrawLine(pen, ToPoint(points[i - 1]), ToPoint(points[i]));
        }
    }

    private static WpfPen CreatePen(StrokeStyle style)
    {
        var pen = new WpfPen(CreateBrush(style), style.Width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
    }

    private static SolidColorBrush CreateBrush(StrokeStyle style)
    {
        var brush = new SolidColorBrush(WpfColor.FromArgb(style.Color.A, style.Color.R, style.Color.G, style.Color.B));
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private static WpfPoint ToPoint(Point2D point) => new(point.X, point.Y);

    private static Rect ToRect(Rect2D rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
