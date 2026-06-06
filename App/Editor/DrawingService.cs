using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DocumentationImageHelper.Editor;

/// <summary>
/// Produces new bitmaps by drawing shapes, freehand strokes, text or crops on
/// top of an existing bitmap. Every result is a 96 DPI Pbgra32 bitmap of the
/// same pixel size as its input, so one device-independent unit always equals
/// one pixel. That keeps screen coordinates and image coordinates identical.
/// </summary>
public static class DrawingService
{
    /// <summary>
    /// Converts an arbitrary incoming bitmap (any DPI or pixel format) into a
    /// 96 DPI Pbgra32 bitmap of the same pixel dimensions. Clipboard images can
    /// arrive at odd DPIs, so normalising up front avoids coordinate skew later.
    /// </summary>
    public static BitmapSource Normalize(BitmapSource source)
    {
        int w = source.PixelWidth;
        int h = source.PixelHeight;
        return RenderToBitmap(w, h, dc => dc.DrawImage(source, new Rect(0, 0, w, h)));
    }

    public static BitmapSource DrawLine(BitmapSource source, Point a, Point b, Color color, double thickness)
        => BakeOnTop(source, dc => dc.DrawLine(MakePen(color, thickness), a, b));

    /// <summary>Bakes a freehand stroke described by a list of points.</summary>
    public static BitmapSource DrawStroke(BitmapSource source, IReadOnlyList<Point> points, Color color, double thickness)
        => BakeOnTop(source, dc =>
        {
            // A single point cannot form a line, so draw a filled dot instead.
            if (points.Count == 1)
            {
                dc.DrawEllipse(new SolidColorBrush(color), null, points[0], thickness / 2, thickness / 2);
                return;
            }

            if (points.Count < 2)
                return;

            dc.DrawGeometry(null, MakePen(color, thickness), BuildPolyline(points));
        });

    public static BitmapSource DrawRectangle(BitmapSource source, Rect rect, Color color, double thickness)
        => BakeOnTop(source, dc => dc.DrawRectangle(null, MakePen(color, thickness), rect));

    public static BitmapSource DrawEllipse(BitmapSource source, Point center, double radiusX, double radiusY, Color color, double thickness)
        => BakeOnTop(source, dc => dc.DrawEllipse(null, MakePen(color, thickness), center, radiusX, radiusY));

    public static BitmapSource DrawText(BitmapSource source, string text, Point origin, Color color, double fontSize)
        => BakeOnTop(source, dc =>
        {
            // pixelsPerDip is 1.0 because the target bitmap is 96 DPI.
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                new SolidColorBrush(color),
                1.0);
            dc.DrawText(formatted, origin);
        });

    /// <summary>Returns a cropped copy of the source restricted to the given region.</summary>
    public static BitmapSource Crop(BitmapSource source, Int32Rect region)
    {
        var cropped = new CroppedBitmap(source, region);
        cropped.Freeze();
        return cropped;
    }

    // Draws the existing image, then runs the caller's drawing on top of it.
    private static BitmapSource BakeOnTop(BitmapSource source, Action<DrawingContext> draw)
    {
        int w = source.PixelWidth;
        int h = source.PixelHeight;
        return RenderToBitmap(w, h, dc =>
        {
            dc.DrawImage(source, new Rect(0, 0, w, h));
            draw(dc);
        });
    }

    // Renders a drawing routine into a fresh frozen bitmap of the given pixel size.
    private static BitmapSource RenderToBitmap(int width, int height, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            draw(dc);

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    // A rounded pen so strokes and shape corners look smooth rather than blocky.
    private static Pen MakePen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    // Builds an open (unfilled, unclosed) polyline geometry from the stroke points.
    private static Geometry BuildPolyline(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], false, false);

            var rest = new List<Point>(points.Count - 1);
            for (int i = 1; i < points.Count; i++)
                rest.Add(points[i]);

            ctx.PolyLineTo(rest, true, true);
        }

        geometry.Freeze();
        return geometry;
    }
}
