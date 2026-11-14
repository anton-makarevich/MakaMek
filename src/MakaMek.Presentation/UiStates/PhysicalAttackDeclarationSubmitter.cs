using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Revalidates and submits a physical-attack declaration. Kept out of
/// <see cref="PhysicalAttackState"/> so the command payload and the eligibility re-check are not part
/// of the state's coupling.
/// </summary>
public sealed class PhysicalAttackDeclarationSubmitter
{
    /// <summary>
    /// Revalidates the pending selection before submitting: a stale selection, a lost permission or an
    /// eligibility change after the dialog opened must not publish a command. Returns whether the
    /// declaration was sent.
    /// </summary>
    public bool TryDeclare(
        IClientGame game,
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

        Submit(game, attacker, new PhysicalAttackDeclarationData
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
    public void Submit(IClientGame game, IUnit attacker, PhysicalAttackDeclarationData? declaration)
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
