using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Controls;

public class HexControl : Panel
{
    private readonly Polygon _hexPolygon;
    private readonly Image _terrainImage;
    private readonly IImageService<Bitmap> _imageService;
    private readonly Hex _hex;

    private static readonly IBrush DefaultStroke = Brushes.White;
    private static readonly IBrush HighlightStroke = new SolidColorBrush(Color.Parse("#00BFFF")); // Light blue
    private static readonly IBrush HighlightFill = new SolidColorBrush(Color.Parse("#3300BFFF")); // Semi-transparent light blue
    private static readonly IBrush TransparentFill = Brushes.Transparent;

    private const double DefaultStrokeThickness = 2;
    private const double HighlightStrokeThickness = 3;

    private static Points GetHexPoints()
    {
        const double width = HexCoordinates.HexWidth;
        const double height = HexCoordinates.HexHeight;

        return new Points([
            new Point(0, height * 0.5),           // Left
            new Point(width * 0.25, height),      // Bottom Left
            new Point(width * 0.75, height),      // Bottom Right
            new Point(width, height * 0.5),       // Right
            new Point(width * 0.75, 0),           // Top Right
            new Point(width * 0.25, 0)            // Top Left
        ]);
    }

    public HexControl(Hex hex, IImageService<Bitmap> imageService)
    {
        _hex = hex;
        _imageService = imageService;
        Width = HexCoordinates.HexWidth;
        Height = HexCoordinates.HexHeight;
        
        // Terrain image (bottom layer)
        _terrainImage = new Image
        {
            Width = Width,
            Height = Height,
            Stretch = Stretch.Fill
        };

        // Hex polygon (top layer)
        _hexPolygon = new Polygon
        {
            Points = GetHexPoints(),
            Fill = TransparentFill,
            Stroke = DefaultStroke,
            StrokeThickness = DefaultStrokeThickness
        };
        
        var label = new Label
        {
            Content = hex.Coordinates.ToString(),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.White
        };
        
        Children.Add(_terrainImage);
        Children.Add(_hexPolygon);
        Children.Add(label);
        
        // Create an observable that polls the unit's position
        Observable
            .Interval(TimeSpan.FromMilliseconds(16)) // ~60fps
            .Select(_ => _hex.IsHighlighted)
            .DistinctUntilChanged()
            .ObserveOn(SynchronizationContext.Current) // Ensure events are processed on the UI thread
            .Subscribe(_ => Highlight(_hex.IsHighlighted ? HexHighlightType.Selected : HexHighlightType.None));
        
        // Set position
        SetValue(Canvas.LeftProperty, hex.Coordinates.H);
        SetValue(Canvas.TopProperty, hex.Coordinates.V);

        UpdateTerrainImage();
    }
    public Hex Hex => _hex;
    
    private void Highlight(HexHighlightType type)
    {
        switch (type)
        {
            case HexHighlightType.Selected:
                _hexPolygon.Stroke = HighlightStroke;
                _hexPolygon.StrokeThickness = HighlightStrokeThickness;
                _hexPolygon.Fill = HighlightFill;
                break;
            case HexHighlightType.None:
            default:
                _hexPolygon.Stroke = DefaultStroke;
                _hexPolygon.StrokeThickness = DefaultStrokeThickness;
                _hexPolygon.Fill = TransparentFill;
                break;
        }
    }

    private void UpdateTerrainImage()
    {
        var terrain = _hex.GetTerrains().FirstOrDefault();
        if (terrain == null) return;

        var image = _imageService.GetImage("terrain", terrain.Id.ToString().ToLower())
            .Result; 
        if (image != null)
        {
            _terrainImage.Source = image;
        }
    }
    
    public bool IsPointInside(Point point)
    {
        // Check if the point is within the bounds
        return Bounds.Contains(point);
    }
}
