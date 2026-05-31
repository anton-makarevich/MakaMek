using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record TerrainMovementCost : MovementCost
{
    public required MakaMekTerrains TerrainId { get; init; }

    public override string Render(ILocalizationService localizationService)
        => string.Format(localizationService.GetString("MovementCost_Terrain"), TerrainId, Value);
}
