using System.Globalization;
using Sanet.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record HexEnterMovementCost : MovementCost
{
    public override string Render(ILocalizationService localizationService)
        => string.Format(CultureInfo.InvariantCulture, localizationService.GetString("MovementCost_HexEnter"), Value);
}
