using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;

/// <summary>
/// Base class for all modifiers (including penalties, which are negative modifiers)
/// </summary>
public abstract record RollModifier
{
    public required int Value { get; init; }

    public abstract string Render(ILocalizationService localizationService);
}
