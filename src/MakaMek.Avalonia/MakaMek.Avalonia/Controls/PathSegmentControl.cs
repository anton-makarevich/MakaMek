using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Avalonia.Controls;

public class PathSegmentControl : Panel
{
    private readonly PathSegmentViewModel _segment;
    private const double StrokeThickness = 2;
    private const double ArrowSize = 15;
    private const double ArcSize = 20;
    private const double EventMarkerRadius = 4;
    private const double EventMarkerSpacing = 12;

    public PathSegmentControl(PathSegmentViewModel segment, Color color)
    {
        _segment = segment;

        Width = HexCoordinatesPixelExtensions.HexWidth * 2;
        Height = HexCoordinatesPixelExtensions.HexHeight * 2;
        var path = new Path
        {
            Stroke = new SolidColorBrush(color),
            StrokeThickness = StrokeThickness,
            Fill = new SolidColorBrush(color),
            Opacity = 0.8,
            Data = CreatePathGeometry()
        };
        
        Children.Add(path);

        AddEventMarkers(color);
        
        SetValue(Canvas.LeftProperty, _segment.FromX - HexCoordinatesPixelExtensions.HexWidth*0.5);
        SetValue(Canvas.TopProperty, _segment.FromY - HexCoordinatesPixelExtensions.HexHeight*0.5);
    }

    private void AddEventMarkers(Color color)
    {
        var events = _segment.Events;
        if (events.Length == 0) return;

        var geometries = new GeometryGroup();
        for (var i = 0; i < events.Length; i++)
        {
            var cx = _segment.EndX - EventMarkerSpacing + i * EventMarkerSpacing;
            var cy = _segment.EndY + EventMarkerRadius + EventMarkerSpacing * 2;
            geometries.Children.Add(GetEventGeometry(events[i].Type, cx, cy));
        }

        Children.Add(new Path
        {
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            Data = geometries
        });
    }

    private static Geometry GetEventGeometry(SegmentEventType eventType, double cx, double cy)
    {
        if (eventType == SegmentEventType.Fall)
        {
            return new GeometryGroup
            {
                Children =
                {
                    new LineGeometry
                    {
                        StartPoint = new Point(cx - EventMarkerRadius, cy - EventMarkerRadius),
                        EndPoint = new Point(cx + EventMarkerRadius, cy + EventMarkerRadius)
                    },
                    new LineGeometry
                    {
                        StartPoint = new Point(cx + EventMarkerRadius, cy - EventMarkerRadius),
                        EndPoint = new Point(cx - EventMarkerRadius, cy + EventMarkerRadius)
                    }
                }
            };
        }

        return new EllipseGeometry
        {
            Center = new Point(cx, cy),
            RadiusX = EventMarkerRadius,
            RadiusY = EventMarkerRadius
        };
    }

    private Geometry CreatePathGeometry()
    {
        var geometry = new GeometryGroup();
        
        if (_segment.IsTurn)
        {
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
        
        for (var i = 0; i < _segment.Cost; i++)
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

                context.BeginFigure(arrowEndPoint);
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
