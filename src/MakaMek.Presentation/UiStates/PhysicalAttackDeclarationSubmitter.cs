using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Revalidates and submits a physical-attack declaration. Exposed as extensions on the client game
/// so <see cref="PhysicalAttackState"/> does not depend on the command payload or the eligibility
/// re-check.
/// </summary>
public static class ClientGamePhysicalAttackExtensions
{
    extension(IClientGame game)
    {
        /// <summary>
        /// Revalidates the pending selection before submitting: a stale selection, a lost permission or an
        /// eligibility change after the dialog opened must not publish a command. Returns whether the
        /// declaration was sent.
        /// </summary>
        public bool TryDeclarePhysicalAttack(
            IUnit attacker,
            IUnit target,
            PhysicalAttackType attackType,
            IReadOnlyList<PartLocation> limbs,
            IReadOnlyList<IUnit> allUnits)
        {
            if (game.BattleMap is not { } map) return false;

            var eligibleLimbs = game.PhysicalAttackEligibility.GetEligibleLimbs(attacker, target, attackType, map);
            if (!limbs.All(eligibleLimbs.Contains)) return false;

            if (game.PhysicalAttackEligibility.GetDeclarationRestriction(
                    attacker, target, attackType, map, allUnits) is not null)
                return false;

            game.SubmitPhysicalAttack(attacker, new PhysicalAttackDeclarationData
            {
                AttackType = attackType,
                AttackerLimbs = limbs.ToList(),
                TargetId = target.Id
            });
            return true;
        }

        /// <summary>
        /// Sends the declaration (or the no-attack command when <paramref name="declaration"/> is null).
        /// The server advances the turn order on that command.
        /// </summary>
        public void SubmitPhysicalAttack(IUnit attacker, PhysicalAttackDeclarationData? declaration)
        {
            var command = new PhysicalAttackCommand
            {
                GameOriginId = game.Id,
                PlayerId = game.PhaseStepState!.Value.ActivePlayer.Id,
                UnitId = attacker.Id,
                Declaration = declaration
            };

            game.DeclarePhysicalAttack(command);
        }
    }
}
