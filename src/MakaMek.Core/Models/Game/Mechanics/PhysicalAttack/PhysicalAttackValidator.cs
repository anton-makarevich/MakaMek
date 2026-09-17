using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Validates the geometry and unit types supported by the initial physical-attack slice.
/// </summary>
public sealed class PhysicalAttackValidator
{
    /// <summary>
    /// Validates a punch or kick between two adjacent deployed BattleMechs.
    /// </summary>
    /// <param name="attacker">The unit declaring the attack.</param>
    /// <param name="target">The intended target.</param>
    /// <param name="attackType">The declared physical attack type.</param>
    /// <returns>A valid result or a diagnostic rejection reason.</returns>
    public PhysicalAttackValidationResult Validate(IUnit? attacker, IUnit? target, PhysicalAttackType attackType)
    {
        if (attacker is not Mech)
            return PhysicalAttackValidationResult.Invalid("Only BattleMechs can make this physical attack.");

        if (target is not Mech)
            return PhysicalAttackValidationResult.Invalid("Only BattleMechs can be targeted by this physical attack.");

        if (attacker.Id == target.Id)
            return PhysicalAttackValidationResult.Invalid("A unit cannot target itself.");

        if (attackType is not (PhysicalAttackType.Punch or PhysicalAttackType.Kick))
            return PhysicalAttackValidationResult.Invalid($"Physical attack type {attackType} is not supported yet.");

        if (attacker.IsDestroyed || target.IsDestroyed)
            return PhysicalAttackValidationResult.Invalid("Destroyed units cannot participate in physical attacks.");

        if (attacker.Position is not { } attackerPosition || target.Position is not { } targetPosition)
            return PhysicalAttackValidationResult.Invalid("Both units must be deployed before attacking.");

        return attackerPosition.Coordinates.DistanceTo(targetPosition.Coordinates) == 1
            ? PhysicalAttackValidationResult.Valid()
            : PhysicalAttackValidationResult.Invalid("Physical attacks require adjacent units.");
    }
}
