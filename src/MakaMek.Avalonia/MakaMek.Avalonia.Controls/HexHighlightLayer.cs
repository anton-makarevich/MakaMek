using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Sanet.MakaMek.Map.Models.Highlights;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// A polygon layer for rendering hex highlights with type-specific appearance configuration.
/// </summary>
public class HexHighlightLayer : Polygon
{
    // Brush configurations by highlight type
    private static readonly IBrush MovementReachableStroke = new SolidColorBrush(Color.Parse("#00BFFF")); // Light blue
    private static readonly IBrush MovementReachableFill = new SolidColorBrush(Color.Parse("#3300BFFF")); // Semi-transparent light blue
    private static readonly IBrush AttackReachableStroke = new SolidColorBrush(Color.Parse("#FFB347")); // Light yellow/orange
    private static readonly IBrush AttackReachableFill = new SolidColorBrush(Color.Parse("#33FFB347")); // Semi-transparent light yellow/orange
    private static readonly IBrush LosBlockingStroke = new SolidColorBrush(Color.Parse("#8B0000")); // Dark red
    private static readonly IBrush LosBlockingFill = new SolidColorBrush(Color.Parse("#338B0000")); // Semi-transparent dark red
    private static readonly IBrush DefaultStroke = Brushes.White;
    private static readonly IBrush TransparentFill = Brushes.Transparent;

    private const double DefaultStrokeThickness = 1;
    private const double HighlightStrokeThickness = 1;

    /// <summary>
    /// Base Z-index for highlight layers.
    /// </summary>
    public const int ZIndexHighlightBase = 25;

    /// <summary>
    /// Creates a new highlight layer with appearance configured for the specified highlight type.
    /// </summary>
    /// <param name="highlightType">The type of highlight to render.</param>
    /// <param name="hexPoints">The polygon points defining the hex shape.</param>
    public HexHighlightLayer(IHexHighlightType highlightType, Points hexPoints)
    {
        Points = hexPoints;
        IsVisible = true;
        ZIndex = ZIndexHighlightBase + highlightType.RenderOrder;

        // Apply brush configuration based on highlight type
        switch (highlightType)
        {
            case MovementReachableHighlight:
                Stroke = MovementReachableStroke;
                Fill = MovementReachableFill;
                StrokeThickness = HighlightStrokeThickness;
                break;
            case AttackReachableHighlight:
                Stroke = AttackReachableStroke;
                Fill = AttackReachableFill;
                StrokeThickness = HighlightStrokeThickness;
                break;
            case LosBlockingHighlight:
                Stroke = LosBlockingStroke;
                Fill = LosBlockingFill;
                StrokeThickness = HighlightStrokeThickness;
                break;
            default:
                // Unknown highlight type - use default appearance
                Stroke = DefaultStroke;
                Fill = TransparentFill;
                StrokeThickness = DefaultStrokeThickness;
                break;
        }
    }
}
