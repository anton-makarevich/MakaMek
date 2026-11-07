using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

/// <summary>
/// The single rule that decides where a unit physically stops for a move. It is used by the model
/// (<c>BaseGame.OnMoveUnit</c>) and by presentation playback, so the displayed position and facing
/// always match the authoritative <c>Unit.Position</c>.
/// </summary>
/// <remarks>
/// A Charge or DFA spends the MP to enter the target's hex and keeps the full path on
/// <c>MovementTaken</c>, but the attacker only enters that hex when the attack resolves. Until then
/// it waits in the last translation's <c>From</c> position: the hex directly in front of the target
/// for a Charge, or the last hex of the jump path for a DFA (Total Warfare, p. 148-149).
/// </remarks>
public static class MovementStopRule
{
    /// <summary>
    /// Whether the declaration is a movement-origin physical attack (Charge or DFA) rather than a
    /// physical attack declared in the Physical Attack Phase.
    /// </summary>
    public static bool IsMovementAttack(PhysicalAttackDeclarationData? declaration) =>
        declaration != null && PhysicalAttackDefinitions.IsMovementAttack(declaration.AttackType);

    /// <summary>
    /// Returns where the unit stops for the given move, or <c>null</c> when it moves to the path's
    /// destination as usual. The stop position is the <c>From</c> of the last coordinate-changing
    /// segment, so trailing turns in the target hex are ignored.
    /// </summary>
    public static HexPosition? GetStopPosition(MovementPath path,
        PhysicalAttackDeclarationData? declaration)
    {
        if (!IsMovementAttack(declaration)) return null;

        var lastTranslationIndex = path.GetLastTranslationIndex();
        return lastTranslationIndex >= 0 ? path.Segments[lastTranslationIndex].From : null;
    }
}
