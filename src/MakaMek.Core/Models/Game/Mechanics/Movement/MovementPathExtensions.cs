using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

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

        /// <summary>
        /// Where the unit grounds when it completes a Death From Above jump, or null when the path
        /// has no translation. The landing is the destination of the last translation with the
        /// approach facing (a jump does not turn in flight, so trailing turns are ignored) and the
        /// airborne level cleared. A bridge deck that no longer exists resolves to the ground
        /// beneath it. The movement path is authoritative: no separate landing position is needed.
        /// </summary>
        /// <param name="map">The battle map, used to validate the declared landing surface.</param>
        public HexPosition? GetDfaLandingPosition(IBattleMap map)
        {
            var lastTranslationIndex = path.GetLastTranslationIndex();
            if (lastTranslationIndex < 0) return null;

            var segment = path.Segments[lastTranslationIndex];
            var surface = segment.To.Surface;
            if (surface == HexSurface.Bridge
                && map.GetHex(segment.To.Coordinates)?.HasTerrain(MakaMekTerrains.Bridge) != true)
                surface = HexSurface.Ground;

            return segment.To with
            {
                Facing = segment.From.Facing,
                Surface = surface,
                AirborneLevel = null
            };
        }
    }
}
