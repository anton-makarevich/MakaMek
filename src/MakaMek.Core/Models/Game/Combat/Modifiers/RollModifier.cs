using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;

/// <summary>
/// Base class for all attack modifiers
/// </summary>
public abstract record RollModifier
{
    public required int Value { get; init; }
    
    public abstract string Format(ILocalizationService localizationService);
}
