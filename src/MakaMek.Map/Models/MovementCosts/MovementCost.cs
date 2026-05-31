using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Map.Models.MovementCosts;

public abstract record MovementCost
{
    public required int Value { get; init; }
    public abstract string Render(ILocalizationService localizationService);
}
