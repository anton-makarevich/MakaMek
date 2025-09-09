using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;

namespace Sanet.MakaMek.Core.Models.Units.Components.Engines;

public class Engine(int rating, EngineType type = EngineType.Fusion)
    : Component($"{type} Engine {rating}", EngineSlots, healthPoints: 3)
{
    public int Rating { get; } = rating;
    public EngineType Type { get; } = type;

    // Engine takes slots 1-3 and 8-10 in CT
    private static readonly int[] EngineSlots = [0, 1, 2, 7, 8, 9];

    /// <summary>
    /// Gets the current heat penalty caused by engine damage.
    /// First hit: +5 heat per turn
    /// Second hit: +10 heat per turn (total)
    /// Third hit: Engine shutdown
    /// </summary>
    public EngineHeatPenalty? HeatPenalty =>
        Hits switch
        {
            1 => new EngineHeatPenalty { EngineHits = 1, Value = 5 },
            2 => new EngineHeatPenalty { EngineHits = 2, Value = 10 },
            _ => null
        };

    public int NumberOfHeatSinks => 10;
    
    public override MakaMekComponent ComponentType => MakaMekComponent.Engine;
}
