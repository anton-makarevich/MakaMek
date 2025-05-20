using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Combat.Modifiers.PilotingSkill;

/// <summary>
/// Modifier for piloting skill rolls due to damaged gyro
/// </summary>
public record DamagedGyroModifier : RollModifier
{
    public required int HitsCount { get; init; }
    
    public override string Format(ILocalizationService localizationService)
    {
        return $"{Value} ({localizationService.GetString("DamagedGyro")} {HitsCount} {localizationService.GetString("Hits")})";
    }
}
