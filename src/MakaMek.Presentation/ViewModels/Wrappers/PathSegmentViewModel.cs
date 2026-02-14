using System.Numerics;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;
using Sanet.MVVM.Core.ViewModels;
using Sanet.MakaMek.Presentation.Models.Map;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class PathSegmentViewModel : BaseViewModel
{
    private readonly PathSegment _segment;
    private const double TurnLength = 40;

    public PathSegmentViewModel(PathSegment segment)
    {
        _segment = segment;
    }
    
    public HexPosition From => _segment.From;
    public HexPosition To => _segment.To;
    public int Cost => _segment.Cost;
    
    public bool IsTurn => From.Coordinates == To.Coordinates && From.Facing != To.Facing;

    // Screen coordinates for positioning the control
    public double FromX => From.Coordinates.H;
    public double FromY => From.Coordinates.V;

    // Relative coordinates for path drawing (center of control is at From position)
    public double StartX => HexCoordinatesPresentationExtensions.HexWidth;
    public double StartY => HexCoordinatesPresentationExtensions.HexHeight;
    
    public double EndX => IsTurn 
        ? StartX + TurnLength * Math.Sin((int)To.Facing * Math.PI / 3)
        : StartX + (To.Coordinates.H - From.Coordinates.H);
        
    public double EndY => IsTurn 
        ? StartY - TurnLength * Math.Cos((int)To.Facing * Math.PI / 3)
        : StartY + (To.Coordinates.V - From.Coordinates.V);

    // Direction vector at the end point (normalized)
    public Vector2 ArrowDirectionVector
    {
        get
        {
           var angle = (int)To.Facing * Math.PI / 3;
           return new Vector2((float)Math.Sin(angle), -(float)Math.Cos(angle));
        }
    }

    public double TurnAngleSweep
    {
        get
        {
            if (!IsTurn) return 0;
            var fromAngle = (int)From.Facing;
            var toAngle = (int)To.Facing;
            
            // For single step turns, we only need to determine if it's clockwise or counterclockwise
            var clockwise = (toAngle - fromAngle + 6) % 6 == 1;
            return clockwise ? 60 : -60;
        }
    }
}
