using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls;

public class PathSegmentControl : Panel
{
    private readonly PathSegmentViewModel _segment;
    private const double StrokeThickness = 2;
    private const double ArrowSize = 15; // Size of arrow head
    private const double ArcSize = 20;

    public PathSegmentControl(PathSegmentViewModel segment, Color color)
    {
        _segment = segment;

        Width = HexCoordinates.HexWidth * 2;
        Height = HexCoordinates.HexHeight * 2;
        var path = new Path
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = StrokeThickness,
            Fill = new SolidColorBrush(color),
            Opacity = 0.8,
            Data = CreatePathGeometry()
        };
        
        Children.Add(path);
        
        // Position control so that From coordinates are at its center
        SetValue(Canvas.LeftProperty, _segment.FromX - HexCoordinates.HexWidth*0.5);
        SetValue(Canvas.TopProperty, _segment.FromY - HexCoordinates.HexHeight*0.5);
    }

    private Geometry CreatePathGeometry()
    {
        var geometry = new GeometryGroup();
        
        if (_segment.IsTurn)
        {
            // Arc from line end to final position
            var arcGeometry = new StreamGeometry();
            using (var context = arcGeometry.Open())
            {
                var sweepAngle = _segment.TurnAngleSweep * Math.PI / 180;
                
                context.BeginFigure(
                    new Point(_segment.StartX, _segment.StartY), 
                    false);
                
                context.ArcTo(
                    new Point(_segment.EndX, _segment.EndY),
                    new Size(ArcSize, ArcSize),
                    0,
                    false,
                    sweepAngle > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise
                );
            }
            geometry.Children.Add(arcGeometry);
        }
        else
        {
            geometry.Children.Add(new LineGeometry
            {
                StartPoint = new Point(_segment.StartX, _segment.StartY),
                EndPoint = new Point(_segment.EndX, _segment.EndY)
            });
        }

        // Add arrows based on Cost value
        var dirX = _segment.ArrowDirectionVector.X;
        var dirY = _segment.ArrowDirectionVector.Y;
        var endPoint = new Point(_segment.EndX, _segment.EndY);
        
        for (int i = 0; i < _segment.Cost; i++)
        {
            var arrowOffset = i * (ArrowSize * 0.5); // Each subsequent arrow is moved back by half-arrow length
            var arrowEndPoint = new Point(
                endPoint.X - arrowOffset * dirX,
                endPoint.Y - arrowOffset * dirY
            );
            
            var arrowGeometry = new StreamGeometry();
            using (var context = arrowGeometry.Open())
            {
                // Calculate arrow points
                var leftPoint = new Point(
                    arrowEndPoint.X - ArrowSize * (dirX * 0.866 - dirY * 0.5),
                    arrowEndPoint.Y - ArrowSize * (dirY * 0.866 + dirX * 0.5)
                );
                var rightPoint = new Point(
                    arrowEndPoint.X - ArrowSize * (dirX * 0.866 + dirY * 0.5),
                    arrowEndPoint.Y - ArrowSize * (dirY * 0.866 - dirX * 0.5)
                );

                context.BeginFigure(arrowEndPoint, true);
                context.LineTo(leftPoint);
                context.LineTo(rightPoint);
                context.LineTo(arrowEndPoint);
                context.EndFigure(true);
                context.SetFillRule(FillRule.NonZero);
            }
            geometry.Children.Add(arrowGeometry);
        }

        return geometry;
    }
}
