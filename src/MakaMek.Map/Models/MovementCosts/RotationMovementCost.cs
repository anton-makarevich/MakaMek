using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record RotationMovementCost : MovementCost
{
    public required HexDirection FromFacing { get; init; }
    public required HexDirection ToFacing { get; init; }

    public override string Render(ILocalizationService localizationService)
    {
        var diff = Math.Abs((int)ToFacing - (int)FromFacing);
        var sides = Math.Min(diff, 6 - diff);
        return string.Format(localizationService.GetString("MovementCost_Rotation"), sides, Value);
    }
}
