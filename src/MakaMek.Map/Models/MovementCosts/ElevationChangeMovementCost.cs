namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record ElevationChangeMovementCost : MovementCost
{
    public required int ElevationDelta { get; init; }
}
