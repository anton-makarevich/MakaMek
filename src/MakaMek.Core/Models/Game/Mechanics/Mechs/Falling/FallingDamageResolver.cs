using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// The damage produced by a fall: the impact damage and, for a skid, the follow-up skid damage.
/// </summary>
public readonly record struct FallingDamageOutcome(FallingDamageData? FallingDamage, FallingDamageData? SkidDamage);

/// <summary>
/// Turns a resolved fall (levels fallen + water depth) into falling/skid damage data.
/// Extracted from <see cref="FallProcessor"/> so the processor does not need to know
/// about the concrete damage shapes.
/// </summary>
public sealed class FallingDamageResolver
{
    private readonly IFallingDamageCalculator _fallingDamageCalculator;

    public FallingDamageResolver(IFallingDamageCalculator fallingDamageCalculator)
    {
        _fallingDamageCalculator = fallingDamageCalculator;
    }

    public FallingDamageOutcome Resolve(
        Mech mech,
        PilotingSkillRollContext context,
        bool isFalling,
        int levelsFallen,
        int waterDepth)
    {
        if (!isFalling) return default;

        // A cliff or DFA fall uses its own fixed facing roll (the DFA roll makes every hit
        // location rear); every other fall rolls the facing.
        var fallingDamageData = context switch
        {
            CliffFallRollContext cliffCtx => _fallingDamageCalculator.CalculateFallingDamage(
                mech, levelsFallen, waterDepth, cliffCtx.FacingDiceRoll, cliffCtx.FacingAfterFall),
            DfaFallRollContext dfaCtx => _fallingDamageCalculator.CalculateFallingDamage(
                mech, levelsFallen, waterDepth, dfaCtx.FacingDiceRoll, dfaCtx.FacingAfterFall),
            _ => _fallingDamageCalculator.CalculateFallingDamage(mech, levelsFallen, waterDepth)
        };

        FallingDamageData? skidDamageData = null;
        if (context is SkidCheckRollContext skidCtx && skidCtx.SkidDistance > 0)
        {
            skidDamageData = _fallingDamageCalculator.CalculateSkidDamage(
                mech,
                skidCtx.SkidDistance,
                fallingDamageData.FacingAfterFall,
                fallingDamageData.FacingDiceRoll,
                fallingDamageData.FallDirection);
        }

        return new FallingDamageOutcome(fallingDamageData, skidDamageData);
    }
}
