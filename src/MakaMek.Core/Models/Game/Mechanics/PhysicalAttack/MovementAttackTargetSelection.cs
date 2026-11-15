using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The unit a movement-origin physical attack resolves against and why it is that unit.
/// </summary>
/// <param name="Target">The unit in the committed destination hex, or null on an automatic failure.</param>
/// <param name="IsReplacementTarget">Whether the target replaced the originally declared one.</param>
/// <param name="IsAutoFail">Whether the attack automatically fails.</param>
public sealed record MovementAttackTargetSelection(
    IUnit? Target,
    bool IsReplacementTarget,
    bool IsAutoFail)
{
    public static MovementAttackTargetSelection AutoFail() => new(null, false, true);
}
