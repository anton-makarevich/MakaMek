using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record HexEnterMovementCost : MovementCost
{
    public override string Render(ILocalizationService localizationService)
        => string.Format(localizationService.GetString("MovementCost_HexEnter"), Value);
}
