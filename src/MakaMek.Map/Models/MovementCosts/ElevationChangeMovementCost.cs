using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public record ElevationChangeMovementCost : MovementCost
{
    public required int ElevationDelta { get; init; }

    public override string Render(ILocalizationService localizationService)
        => string.Format(localizationService.GetString("MovementCost_ElevationChange"), ElevationDelta, Value);
}
