namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Resolves the attacker-state modifier of the Attack Modifiers Table.
/// <para>
/// The attacker movement rows of the table (stationary / walked / ran / jumped / prone /
/// skidding) are <b>exclusive states</b>: exactly one of them applies to the attacker's
/// movement for the turn. A prone (+2) or skidding (+1) attacker-state modifier therefore
/// <b>replaces</b> the movement-type modifier (including Walk, Run and Jump) instead of
/// stacking on top of it.
/// </para>
/// <para>
/// When the attacker is both prone and skidding (a 'Mech commonly falls at the end of a
/// skid), <b>prone wins</b>: the unit's final movement state is prone, so the Prone (+2)
/// modifier applies and no skidding modifier is emitted.
/// </para>
/// <para>
/// Four-legged 'Mechs are not modelled in this codebase (the prone row does not apply to
/// them, and skidding does not apply to unintentional charges — neither concept exists here).
/// </para>
/// </summary>
public static class AttackerStateModifierResolver
{
    /// <summary>
    /// Resolves the single applicable attacker-state modifier from the exclusive attacker
    /// movement rows of the Attack Modifiers Table.
    /// </summary>
    /// <param name="isProne">Whether the attacking unit is prone.</param>
    /// <param name="isSkidding">Whether the attacking unit is skidding.</param>
    /// <returns>
    /// A <see cref="ProneAttackerModifier"/> (+2) when prone, otherwise a
    /// <see cref="SkiddingAttackerModifier"/> (+1) when skidding, otherwise null.
    /// </returns>
    public static RollModifier? Resolve(bool isProne, bool isSkidding)
    {
        if (isProne)
        {
            return new ProneAttackerModifier
            {
                Value = ProneAttackerModifier.DefaultValue // +2 replaces the movement-type modifier
            };
        }

        if (isSkidding)
        {
            return new SkiddingAttackerModifier
            {
                Value = SkiddingAttackerModifier.DefaultValue // +1 replaces the movement-type modifier
            };
        }

        return null;
    }

    /// <summary>
    /// Determines whether the given modifier is an attacker-state modifier
    /// (<see cref="ProneAttackerModifier"/> or <see cref="SkiddingAttackerModifier"/>),
    /// which replaces the attacker movement-type modifier in a breakdown.
    /// </summary>
    public static bool IsAttackerStateModifier(RollModifier modifier) =>
        modifier is ProneAttackerModifier or SkiddingAttackerModifier;
}
