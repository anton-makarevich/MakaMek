using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

/// <summary>
/// Movement penalty applied due to destroyed legs
/// </summary>
public record LegDestroyedPenalty : RollModifier
{
    /// <summary>
    /// Number of destroyed legs 
    /// </summary>
    public required int DestroyedLegCount { get; init; }
    
    /// <summary>
    /// Base walking movement points before any penalties
    /// </summary>
    public required int BaseWalkingMp { get; init; }
    
    /// <summary>
    /// Creates a leg destroyed penalty with the correct penalty value based on destroyed leg count and base movement
    /// </summary>
    /// <param name="destroyedLegCount">Number of destroyed legs</param>
    /// <param name="baseWalkingMp">Base walking movement points</param>
    /// <returns>Leg destroyed penalty with calculated value</returns>
    public static LegDestroyedPenalty? Create(int destroyedLegCount, int baseWalkingMp)
    {
        
        var penaltyValue = destroyedLegCount switch
        {
            0 => 0,
            1 => baseWalkingMp - 1, // Penalty to achieve 1 MP
            2 => baseWalkingMp, // 2+ hips destroyed = movement to 0, so penalty equals base movement
            _ => 0
        };
        
        if (penaltyValue == 0) return null;
        
        return new LegDestroyedPenalty
        {
            DestroyedLegCount = destroyedLegCount,
            BaseWalkingMp = baseWalkingMp,
            Value = penaltyValue
        };
    }
    
    public override string Render(ILocalizationService localizationService)
    {
        return DestroyedLegCount switch
        {
            0 => string.Empty,
            1 => string.Format(localizationService.GetString("Penalty_LegDestroyed_Single"), Value),
            _ => localizationService.GetString("Penalty_LegDestroyed_Both")
        };
    }
}
