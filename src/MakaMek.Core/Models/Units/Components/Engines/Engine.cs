using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;

namespace Sanet.MakaMek.Core.Models.Units.Components.Engines;

public class Engine(int rating, EngineType type = EngineType.Fusion)
    : Component($"{type} Engine {rating}",  GetEngineSize(type), healthPoints: 3)
{
    public int Rating { get; } = rating;
    public EngineType Type { get; } = type;

    private static int GetEngineSize(EngineType type)
    {
        return type switch
        {
            EngineType.Fusion => 6,      // Standard fusion: 6 slots in CT
            EngineType.XLFusion => 10,   // XL fusion: 6 slots in CT + 4 slots in side torsos
            EngineType.Light => 4,       // Light engine: 4 slots in CT
            EngineType.Compact => 3,     // Compact engine: 3 slots in CT
            EngineType.ICE => 6,         // ICE engine: 6 slots in CT
            _ => 6
        };
    }

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
