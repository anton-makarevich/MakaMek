using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record JumpMovementCost : MovementCost
{
    public override string Render(ILocalizationService localizationService)
        => string.Format(localizationService.GetString("MovementCost_Jump"), Value);
}
