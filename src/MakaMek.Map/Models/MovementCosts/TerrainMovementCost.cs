using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

/// <summary>
/// Represents only the additional terrain-specific movement cost beyond the base hex entry cost.
/// For example, light woods adds 1 MP on top of the standard 1 MP entry cost.
/// </summary>
public record TerrainMovementCost : MovementCost
{
    public required MakaMekTerrains TerrainId { get; init; }
    public int? Depth { get; init; }

    public override string Render(ILocalizationService localizationService)
    {
        var terrainName = localizationService.GetString($"Terrain_{TerrainId}");
        if (TerrainId == MakaMekTerrains.Water && Depth.HasValue)
        {
            return string.Format(
                localizationService.GetString("MovementCost_Terrain_Water"),
                terrainName, Depth.Value, Value);
        }
        return string.Format(
            localizationService.GetString("MovementCost_Terrain"),
            terrainName, Value);
    }
}
