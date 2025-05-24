using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// Represents a modifier to piloting skill rolls based on the number of levels fallen
/// </summary>
public record FallingLevelsModifier : RollModifier
{
    /// <summary>
    /// The number of levels the mech fell
    /// </summary>
    public required int LevelsFallen { get; init; }
    
    public override string Format(ILocalizationService localizationService) => 
        $"{Value} ({localizationService.GetString("Modifier_FallingLevels")} {LevelsFallen} {localizationService.GetString("Levels")})";
}
