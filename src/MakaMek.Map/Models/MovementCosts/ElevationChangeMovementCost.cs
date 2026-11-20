using System.Globalization;
using Sanet.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record ElevationChangeMovementCost : MovementCost
{
    public required int ElevationDelta { get; init; }

    public override string Render(ILocalizationService localizationService)
        => string.Format(CultureInfo.InvariantCulture,
            localizationService.GetString("MovementCost_ElevationChange"), ElevationDelta, Value);
}
