using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record TerrainMovementCost : MovementCost
{
    public required MakaMekTerrains TerrainId { get; init; }
}
