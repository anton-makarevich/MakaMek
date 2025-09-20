using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;

namespace Sanet.MakaMek.Core.Models.Units.Components.Engines;

public class Engine : Component
{
    public int Rating { get; }
    public EngineType Type { get; }

    public Engine(int rating, EngineType type = EngineType.Fusion, ComponentData? componentData = null)
        : base(new EngineDefinition(type), componentData)
    {
        Rating = rating;
        Type = type;
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

    protected override ComponentSpecificData? GetSpecificData()
    {
        return new EngineStateData(Rating, Type);
    }
}
