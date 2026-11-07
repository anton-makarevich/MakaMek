using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;

/// <summary>
/// Movement rules that need more than the path itself, shared by the model and presentation playback.
/// </summary>
public static class MovementPathExtensions
{
    /// <param name="path">The path the move followed.</param>
    extension(MovementPath path)
    {
        /// <summary>
        /// Returns where the unit stops for this move, or <c>null</c> when it moves to the path's
        /// destination as usual. A Charge or DFA spends the MP to enter the target's hex and keeps
        /// the full path on <c>MovementTaken</c>, but the attacker waits in the last translation's
        /// <c>From</c> position until the attack resolves: the hex directly in front of the target
        /// for a Charge, or the last hex of the jump path for a DFA (Total Warfare, p. 148-149).
        /// Trailing turns in the target hex are therefore ignored.
        /// </summary>
        /// <param name="declaration">The move's Charge or DFA declaration, when it has one.</param>
        public HexPosition? GetStopPosition(PhysicalAttackDeclarationData? declaration)
        {
            if (!declaration.IsMovementAttack()) return null;

            var lastTranslationIndex = path.GetLastTranslationIndex();
            return lastTranslationIndex >= 0 ? path.Segments[lastTranslationIndex].From : null;
        }
    }
}
