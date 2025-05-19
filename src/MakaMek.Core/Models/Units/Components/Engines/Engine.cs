using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Engines;

public class Engine : Component
{
    public int Rating { get; }
    public EngineType Type { get; }

    // Engine takes slots 1-3 and 8-10 in CT
    private static readonly int[] EngineSlots = [0, 1, 2, 7, 8, 9];

    /// <summary>
    /// Gets the current heat penalty caused by engine damage.
    /// First hit: +5 heat per turn
    /// Second hit: +10 heat per turn (total)
    /// Third hit: Engine shutdown
    /// </summary>
    public int HeatPenalty => Hits switch
    {
        1 => 5,   // First hit (1 point of shielding destroyed)
        2 => 10,  // Second hit (2 points of shielding destroyed)
        _ => 0    // No hits or 3 hits (engine shutdown handled separately)
    };

    public Engine(int rating, EngineType type = EngineType.Fusion) 
        : base($"{type} Engine {rating}", EngineSlots, healthPoints:3)
    {
        Rating = rating;
        Type = type;
    }
    
    public int NumberOfHeatSinks => 10;
    
    public override MakaMekComponent ComponentType => MakaMekComponent.Engine;
}
