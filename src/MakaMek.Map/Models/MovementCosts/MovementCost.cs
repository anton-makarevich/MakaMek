namespace Sanet.MakaMek.Map.Models.MovementCosts;

public abstract record MovementCost
{
    public required int Value { get; init; }
}
