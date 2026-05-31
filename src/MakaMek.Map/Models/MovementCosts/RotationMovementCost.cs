namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record RotationMovementCost : MovementCost
{
    public required HexDirection FromFacing { get; init; }
    public required HexDirection ToFacing { get; init; }
}
