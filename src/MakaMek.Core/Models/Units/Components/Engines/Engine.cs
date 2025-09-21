using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;

namespace Sanet.MakaMek.Core.Models.Units.Components.Engines;

public class Engine : Component
{
    public int Rating { get; }
    public EngineType Type { get; }

    public Engine(ComponentData componentData)
        : base(CreateEngineDefinition(
            ((EngineStateData)componentData.SpecificData!).Type,
            ((EngineStateData)componentData.SpecificData!).Rating), componentData)
    {
        if (componentData.SpecificData is not EngineStateData engineState)
        {
            throw new ArgumentException("Invalid component data for engine");
        }
        Rating = engineState.Rating;
        Type = engineState.Type;
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

    protected override ComponentSpecificData GetSpecificData()
    {
        return new EngineStateData(Rating, Type);
    }
    
    public static EngineDefinition CreateEngineDefinition(EngineType type, int engineRating)
    {
        return new EngineDefinition(type, engineRating);
    }
}
