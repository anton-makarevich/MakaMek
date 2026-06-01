using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record RotationMovementCost : MovementCost
{
    public required HexDirection FromFacing { get; init; }
    public required HexDirection ToFacing { get; init; }

    public override string Render(ILocalizationService localizationService)
    {
        var sides = FromFacing.ShortestRotationTo(ToFacing);
        return string.Format(localizationService.GetString("MovementCost_Rotation"), sides, Value);
    }
}
