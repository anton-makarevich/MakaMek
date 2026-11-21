using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Eligibility-backed predicates used by the physical-attack selection UI. Exposed as extensions on
/// the unit/game they describe so the stateful <see cref="PhysicalAttackTargeting"/> only keeps the
/// reachable-target set.
/// </summary>
public static class PhysicalAttackTargetingExtensions
{
    extension(IUnit unit)
    {
        /// <summary>
        /// A unit can act as the attacker while it belongs to the active player and the eligibility
        /// policy still reports an option for it. The policy owns the "not destroyed, not already
        /// committed" rules.
        /// </summary>
        public bool CanActAsPhysicalAttacker(IClientGame game, IEnumerable<IUnit> units) =>
            unit.IsFriendly(game) && unit.HasAnyPhysicalAttackOption(game, units);

        public bool IsFriendly(IClientGame game) =>
            unit.Owner?.Id == game.PhaseStepState?.ActivePlayer.Id;

        public bool HasAnyPhysicalAttackOption(IClientGame game, IEnumerable<IUnit> units)
        {
            if (game.BattleMap is not { } map) return false;

            // Evaluated against the candidate, not the current attacker: this runs before the
            // candidate becomes the attacker. The policy filters enemies and unit states itself.
            return game.PhysicalAttackEligibility.HasAnyOption(unit, units, map);
        }
    }

    extension(IClientGame game)
    {
        public List<IUnit> GetAllPlayerUnits() =>
            game.Players.SelectMany(player => player.Units).ToList();
    }
}
