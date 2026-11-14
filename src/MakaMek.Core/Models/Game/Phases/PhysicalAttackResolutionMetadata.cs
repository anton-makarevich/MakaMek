using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// Records the parts and the destruction a physical attack resolution caused on both units.
/// </summary>
internal static class PhysicalAttackResolutionMetadata
{
    /// <summary>
    /// Records the parts and the destruction a resolution caused on both units. The target's
    /// metadata stays on the attack resolution; a charge's attacker metadata travels in its
    /// charge sub-record so clients can show both units' results.
    /// </summary>
    public static PhysicalAttackResolutionData AttachDestructionMetadata(
        PhysicalAttackResolutionData resolution,
        IUnit target,
        IReadOnlyList<PartLocation> targetDestroyedPartsBefore,
        bool targetWasDestroyedBefore,
        IUnit attacker,
        IReadOnlyList<PartLocation> attackerDestroyedPartsBefore,
        bool attackerWasDestroyedBefore)
    {
        var newlyDestroyedTargetParts = GetDestroyedParts(target)
            .Except(targetDestroyedPartsBefore).ToList();
        resolution = resolution with
        {
            AttackResolution = resolution.AttackResolution with
            {
                DestroyedParts = newlyDestroyedTargetParts.Count != 0 ? newlyDestroyedTargetParts : null,
                UnitDestroyed = !targetWasDestroyedBefore && target.IsDestroyed
            }
        };

        if (resolution.Charge is not { } charge) return resolution;

        var newlyDestroyedAttackerParts = GetDestroyedParts(attacker)
            .Except(attackerDestroyedPartsBefore).ToList();
        return resolution with
        {
            Charge = charge with
            {
                AttackerDestroyedParts = newlyDestroyedAttackerParts.Count != 0
                    ? newlyDestroyedAttackerParts
                    : null,
                AttackerUnitDestroyed = !attackerWasDestroyedBefore && attacker.IsDestroyed
            }
        };
    }

    public static List<PartLocation> GetDestroyedParts(IUnit unit) =>
        unit.Parts.Values.Where(part => part.IsDestroyed).Select(part => part.Location).ToList();
}
