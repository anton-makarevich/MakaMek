using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

/// <summary>
/// Movement penalty applied due to destroyed hip actuators
/// Special case: 1 hip = movement halved, 2+ hips = movement to 0
/// </summary>
public record HipDestroyedPenalty : RollModifier
{
    /// <summary>
    /// Number of destroyed hip actuators
    /// </summary>
    public required int DestroyedHipCount { get; init; }
    
    /// <summary>
    /// Base walking movement points before any penalties
    /// </summary>
    public required int BaseWalkingMp { get; init; }
    
    /// <summary>
    /// Creates a hip destroyed penalty with the correct penalty value based on destroyed hip count and base movement
    /// </summary>
    /// <param name="destroyedHipCount">Number of destroyed hip actuators</param>
    /// <param name="baseWalkingMp">Base walking movement points</param>
    /// <returns>Hip destroyed penalty with calculated value</returns>
    public static HipDestroyedPenalty? Create(int destroyedHipCount, int baseWalkingMp)
    {
        var penaltyValue = destroyedHipCount switch
        {
            1 => baseWalkingMp - (int)Math.Ceiling(baseWalkingMp * 0.5), // Penalty to achieve halved movement
            2 => baseWalkingMp, // both hips destroyed = movement to 0, so penalty equals base movement
            _ => 0 
        };
        if (penaltyValue == 0) return null;
        
        return new HipDestroyedPenalty
        {
            DestroyedHipCount = destroyedHipCount,
            BaseWalkingMp = baseWalkingMp,
            Value = penaltyValue
        };
    }
    
    public override string Render(ILocalizationService localizationService)
    {
        return DestroyedHipCount switch
        {
            0 => string.Empty,
            1 => string.Format(localizationService.GetString("Penalty_HipDestroyed_Single"), Value),
            _ => localizationService.GetString("Penalty_HipDestroyed_Both")
        };
    }
}
