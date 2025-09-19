using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Definition for engine components with variable size based on engine type
/// </summary>
public record EngineDefinition(
    EngineType Type,
    int NumberOfHeatSinks = 10)
    : ComponentDefinition(
        Name: $"{Type} Engine",
        Size: GetEngineSize(Type),
        HealthPoints: 3,
        BattleValue: 0,
        IsRemovable: true,
        ComponentType: MakaMekComponent.Engine)
{
    /// <summary>
    /// Gets the size in critical slots for the given engine type
    /// </summary>
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
}