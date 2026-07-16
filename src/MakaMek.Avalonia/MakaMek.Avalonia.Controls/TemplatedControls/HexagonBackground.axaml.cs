using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Sanet.MakaMek.Avalonia.Controls.TemplatedControls;

public class HexagonBackground : Control
{
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<HexagonBackground, IBrush?>(nameof(Stroke),
            new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<HexagonBackground, double>(nameof(StrokeThickness), 1.0);

    public static readonly StyledProperty<double> PatternScaleProperty =
        AvaloniaProperty.Register<HexagonBackground, double>(nameof(PatternScale), 2.0);

    static HexagonBackground()
    {
        AffectsRender<HexagonBackground>(StrokeProperty, StrokeThicknessProperty, PatternScaleProperty);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public double PatternScale
    {
        get => GetValue(PatternScaleProperty);
        set => SetValue(PatternScaleProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || PatternScale <= 0) return;

        var pen = new Pen(Stroke, StrokeThickness);

        const double cellWidth = 150.0;
        const double cellHeight = 86.6;

        var sw = cellWidth * PatternScale;
        var sh = cellHeight * PatternScale;

        ReadOnlySpan<Point> hex1 =
        [
            new(25, 0), new(75, 0), new(100, 43.3),
            new(75, 86.6), new(25, 86.6), new(0, 43.3)
        ];

        ReadOnlySpan<Point> hex2 =
        [
            new(100, 43.3), new(150, 43.3), new(175, 86.6),
            new(150, 130), new(100, 130), new(75, 86.6)
        ];

        var cols = (int)Math.Ceiling(bounds.Width / sw) + 2;
        var rows = (int)Math.Ceiling(bounds.Height / sh) + 2;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var row = -1; row <= rows; row++)
            {
                var oy = row * sh;
                for (var col = -1; col <= cols; col++)
                {
                    var ox = col * sw;
                    AppendHexagon(ctx, hex1, ox, oy, PatternScale);
                    AppendHexagon(ctx, hex2, ox, oy, PatternScale);
                }
            }
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static void AppendHexagon(StreamGeometryContext ctx, ReadOnlySpan<Point> vertices,
        double offsetX, double offsetY, double scale)
    {
        ctx.BeginFigure(new Point(vertices[0].X * scale + offsetX, vertices[0].Y * scale + offsetY));
        for (var i = 1; i < vertices.Length; i++)
        {
            ctx.LineTo(new Point(vertices[i].X * scale + offsetX, vertices[i].Y * scale + offsetY));
        }
        ctx.EndFigure(true);
    }

    
}
