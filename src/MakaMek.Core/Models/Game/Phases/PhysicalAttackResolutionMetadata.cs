using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Records the parts and the destruction a physical attack resolution caused on both units.
/// </summary>
internal static class PhysicalAttackResolutionMetadata
{
    extension(PhysicalAttackResolutionData resolution)
    {
        /// <summary>
        /// Records the parts and the destruction a resolution caused on both units. The target's
        /// metadata stays on the attack resolution; a charge's or DFA's attacker metadata travels in
        /// its sub-record so clients can show both units' results.
        /// </summary>
        public PhysicalAttackResolutionData AttachDestructionMetadata(
            IUnit? target,
            IReadOnlyList<PartLocation> targetDestroyedPartsBefore,
            bool targetWasDestroyedBefore,
            IUnit attacker,
            IReadOnlyList<PartLocation> attackerDestroyedPartsBefore,
            bool attackerWasDestroyedBefore)
        {
            if (target is not null)
            {
                var newlyDestroyedTargetParts = target.GetDestroyedParts()
                    .Except(targetDestroyedPartsBefore).ToList();
                resolution = resolution with
                {
                    AttackResolution = resolution.AttackResolution with
                    {
                        DestroyedParts = newlyDestroyedTargetParts.Count != 0 ? newlyDestroyedTargetParts : null,
                        UnitDestroyed = !targetWasDestroyedBefore && target.IsDestroyed
                    }
                };
            }

            var newlyDestroyedAttackerParts = attacker.GetDestroyedParts()
                .Except(attackerDestroyedPartsBefore).ToList();
            var attackerUnitDestroyed = !attackerWasDestroyedBefore && attacker.IsDestroyed;

            if (resolution.Charge is { } charge)
            {
                return resolution with
                {
                    Charge = charge with
                    {
                        AttackerDestroyedParts = newlyDestroyedAttackerParts.Count != 0
                            ? newlyDestroyedAttackerParts
                            : null,
                        AttackerUnitDestroyed = attackerUnitDestroyed
                    }
                };
            }

            if (resolution.Dfa is { } dfa)
            {
                return resolution with
                {
                    Dfa = dfa with
                    {
                        AttackerDestroyedParts = newlyDestroyedAttackerParts.Count != 0
                            ? newlyDestroyedAttackerParts
                            : null,
                        AttackerUnitDestroyed = attackerUnitDestroyed
                    }
                };
            }

            return resolution;
        }
    }

    extension(IUnit unit)
    {
        public List<PartLocation> GetDestroyedParts() =>
            unit.Parts.Values.Where(part => part.IsDestroyed).Select(part => part.Location).ToList();
    }
}
