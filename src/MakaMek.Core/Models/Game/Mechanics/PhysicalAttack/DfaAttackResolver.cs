using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Pure mechanics resolution of a Death From Above (Total Warfare, p. 150): the to-hit number comes
/// from the physical attack calculator, the hit is rolled on 2D6, and the damage is resolved in
/// 5-point groups on the Punch Location Table for the target and on the Front column of the Kick
/// Location Table for the attacker's legs.
/// <para>
/// The attack direction and the push vector are measured from the hex where the jumping 'Mech
/// started its movement, not from the airborne approach hex.
/// </para>
/// <para>
/// The resolver never mutates a unit: applying the damage and rolling the critical hits stays with
/// the caller, exactly as with weapon hits.
/// </para>
/// </summary>
public class DfaAttackResolver(
    IPhysicalAttackCalculator toHitCalculator,
    IDiceRoller diceRoller,
    IRulesProvider rulesProvider,
    IDamageTransferCalculator damageTransferCalculator) : IDfaAttackResolver
{
    /// <inheritdoc />
    public PhysicalAttackResolutionData ResolveDfa(DfaAttackContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Map);

        if (context.Attacker.Position is null)
            throw new InvalidOperationException("Attacker position is not set");
        if (context.Target.Position is null)
            throw new InvalidOperationException("Target position is not set");

        var toHitNumber = toHitCalculator.GetDfaToHitNumber(
            context.Attacker,
            context.Target,
            context.Map);

        var attackRoll = diceRoller.Roll2D6();
        var isHit = attackRoll.Sum(d => d.Result) >= toHitNumber;
        var psrRequirements = rulesProvider.GetPhysicalAttackPsrRequirements(
            PhysicalAttackType.DFA,
            isHit);

        if (!isHit)
        {
            return new PhysicalAttackResolutionData(
                PhysicalAttackType.DFA,
                null,
                0,
                PhysicalAttackRolls.CreateAttackResolution(
                    toHitNumber, attackRoll, isHit: false, HitDirection.Front, null),
                psrRequirements,
                Dfa: new DfaResolutionData(
                    DfaOutcome.Miss,
                    OriginalTargetId: context.Target.Id));
        }

        var attackDirection = context.Target.GetAttackDirectionFrom(context.StartCoordinates);
        var targetIsProne = context.Target.Status.HasFlag(UnitStatus.Prone);
        var targetDamage = rulesProvider.GetDfaDamage(context.Attacker.Tonnage);
        var targetHitLocations = ResolveTargetDamage(
            context.Target,
            targetDamage,
            attackDirection);

        // The attacker takes leg damage on the Front column of the Kick Location Table.
        var attackerDamage = rulesProvider.GetDfaAttackerDamage(context.Attacker.Tonnage);
        var attackerLegHitLocations = ResolveClusters(
            context.Attacker,
            attackerDamage,
            HitDirection.Front,
            PhysicalAttackType.Kick,
            useTwoDice: false);

        return new PhysicalAttackResolutionData(
            PhysicalAttackType.DFA,
            null,
            targetDamage,
            PhysicalAttackRolls.CreateAttackResolution(
                toHitNumber,
                attackRoll,
                isHit: true,
                attackDirection,
                targetHitLocations,
                damageDirection: targetIsProne ? HitDirection.Rear : null),
            psrRequirements,
            Dfa: new DfaResolutionData(
                DfaOutcome.Hit,
                targetHitLocations,
                attackerLegHitLocations,
                context.Target.Id));
    }

    /// <summary>
    /// Resolves DFA target damage: 5-point groups on the Punch table, or the rear column of the
    /// standard 'Mech Hit Location Table when the target is prone (Total Warfare, p. 150).
    /// </summary>
    private AttackHitLocationsData? ResolveTargetDamage(
        IUnit target,
        int damage,
        HitDirection direction)
    {
        if (damage <= 0) return null;

        return target.Status.HasFlag(UnitStatus.Prone)
            ? ResolveClusters(target, damage, HitDirection.Rear, tableType: null, useTwoDice: true)
            : ResolveClusters(target, damage, direction, PhysicalAttackType.DFA, useTwoDice: false);
    }

    /// <summary>
    /// Splits damage into 5-point groups plus a remainder and rolls the hit location table once
    /// per group. A physical attack table uses 1D6; the standard 'Mech Hit Location Table uses
    /// 2D6. <paramref name="tableType"/> null selects the standard table.
    /// </summary>
    private AttackHitLocationsData? ResolveClusters(
        IUnit unit,
        int damage,
        HitDirection direction,
        PhysicalAttackType? tableType,
        bool useTwoDice)
    {
        if (damage <= 0) return null;

        var hitLocations = new List<LocationHitData>();
        var totalDamage = 0;

        foreach (var groupDamage in PhysicalAttackRolls.SplitIntoFivePointGroups(damage))
        {
            var locationRoll = useTwoDice
                ? diceRoller.Roll2D6()
                : new List<DiceResult> { diceRoller.RollD6() };
            var roll = locationRoll.Sum(d => d.Result);
            var rolledLocation = tableType is { } attackType
                ? rulesProvider.GetPhysicalAttackHitLocation(attackType, roll, direction)
                : rulesProvider.GetHitLocation(roll, direction);
            var hitLocation = PhysicalAttackRolls.ResolveTransferLocation(unit, rolledLocation);

            var damageData = damageTransferCalculator.CalculateStructureDamage(
                unit,
                hitLocation,
                groupDamage,
                direction,
                hitLocations);

            hitLocations.Add(new LocationHitData(
                damageData,
                [],
                locationRoll.Select(d => d.Result).ToArray(),
                rolledLocation));
            totalDamage += groupDamage;
        }

        return new AttackHitLocationsData(hitLocations, totalDamage, [], 0);
    }
}
