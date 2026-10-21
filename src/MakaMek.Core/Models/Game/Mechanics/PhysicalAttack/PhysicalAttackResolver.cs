using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Pure mechanics resolution of a punch or a kick for a single attacking limb. The resolver
/// follows the resolution order of the weapon resolver: the to-hit number comes from the
/// physical attack calculator, the hit is rolled on 2D6, the direction of the attack is taken
/// from the target's facing, the hit location is rolled on 1D6 on the punch or kick table and the
/// damage is distributed by the standard damage transfer calculator.
/// The resolver never mutates a unit: applying the damage and rolling the critical hits stays
/// with the caller, exactly as with weapon hits.
/// </summary>
public class PhysicalAttackResolver : IPhysicalAttackResolver
{
    private readonly IPhysicalAttackCalculator _toHitCalculator;
    private readonly IDiceRoller _diceRoller;
    private readonly IRulesProvider _rulesProvider;
    private readonly IDamageTransferCalculator _damageTransferCalculator;

    public PhysicalAttackResolver(
        IPhysicalAttackCalculator toHitCalculator,
        IDiceRoller diceRoller,
        IRulesProvider rulesProvider,
        IDamageTransferCalculator damageTransferCalculator)
    {
        _toHitCalculator = toHitCalculator;
        _diceRoller = diceRoller;
        _rulesProvider = rulesProvider;
        _damageTransferCalculator = damageTransferCalculator;
    }

    public PhysicalAttackResolutionData ResolveAttack(PhysicalAttackContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Map);

        ValidateAttackType(context.AttackType);
        ValidateLimb(context.AttackType, context.AttackerLimb);

        if (context.Attacker.Position is null)
            throw new InvalidOperationException("Attacker position is not set");
        if (context.Target.Position is null)
            throw new InvalidOperationException("Target position is not set");

        var damage = CalculateDamage(context.Attacker.Tonnage, context.AttackType);

        var toHitNumber = _toHitCalculator.GetToHitNumber(
            context.Attacker,
            context.Target,
            context.AttackType,
            context.Map);

        var attackRoll = _diceRoller.Roll2D6();
        var isHit = attackRoll.Sum(d => d.Result) >= toHitNumber;

        if (!isHit)
        {
            return new PhysicalAttackResolutionData(
                context.AttackType,
                context.AttackerLimb,
                damage,
                new AttackResolutionData(toHitNumber, attackRoll, false, HitDirection.Front, 0),
                RequiresTargetPsr: false,
                RequiresAttackerPsr: context.AttackType == PhysicalAttackType.Kick);
        }

        var attackDirection = context.Target.GetAttackDirection(context.Attacker);

        var locationRoll = _diceRoller.RollD6();
        var rolledLocation = GetHitLocation(context.AttackType, locationRoll.Result, attackDirection);
        var hitLocation = ResolveTransferLocation(context.Target, rolledLocation);

        var damageData = _damageTransferCalculator.CalculateStructureDamage(
            context.Target,
            hitLocation,
            damage,
            attackDirection);

        var locationHitData = new LocationHitData(
            damageData,
            [],
            [locationRoll.Result],
            rolledLocation);

        var hitLocationsData = new AttackHitLocationsData(
            [locationHitData],
            damage,
            [],
            0);

        return new PhysicalAttackResolutionData(
            context.AttackType,
            context.AttackerLimb,
            damage,
            new AttackResolutionData(toHitNumber, attackRoll, true, attackDirection, 0, hitLocationsData),
            RequiresTargetPsr: context.AttackType == PhysicalAttackType.Kick,
            RequiresAttackerPsr: false);
    }

    /// <summary>
    /// The damage value of a physical attack: one point of damage for every ten tons of the
    /// attacker's weight for a punch and every five tons for a kick, rounded up.
    /// </summary>
    /// <param name="tonnage">The tonnage of the attacking unit.</param>
    /// <param name="attackType">The kind of physical attack.</param>
    /// <returns>The damage value of the attack.</returns>
    private static int CalculateDamage(int tonnage, PhysicalAttackType attackType)
    {
        var divisor = attackType == PhysicalAttackType.Punch ? 10 : 5;
        return (tonnage + divisor - 1) / divisor;
    }

    /// <summary>
    /// The hit location of a physical attack, rolled on 1D6 on the punch or the kick table. The
    /// column comes from the direction the attack enters the target's hex.
    /// </summary>
    private PartLocation GetHitLocation(
        PhysicalAttackType attackType,
        int locationRoll,
        HitDirection attackDirection)
    {
        return attackType switch
        {
            PhysicalAttackType.Punch => _rulesProvider.GetPunchHitLocation(locationRoll, attackDirection),
            PhysicalAttackType.Kick => _rulesProvider.GetKickHitLocation(locationRoll, attackDirection),
            _ => throw new NotSupportedException(
                $"Physical attack resolution is not supported for attack type: {attackType}")
        };
    }

    /// <summary>
    /// Follows the damage transfer chain of an already destroyed location, as the weapon
    /// resolver does, so a hit on a blown off limb lands on the next intact location.
    /// </summary>
    private static PartLocation ResolveTransferLocation(IUnit target, PartLocation rolledLocation)
    {
        var hitLocation = rolledLocation;
        var visited = new HashSet<PartLocation> { rolledLocation };

        while (target.Parts.TryGetValue(hitLocation, out var part) && part.IsDestroyed)
        {
            var nextLocation = part.GetNextTransferLocation();
            if (nextLocation == null || !visited.Add(nextLocation.Value))
                break;

            hitLocation = nextLocation.Value;
        }

        return hitLocation;
    }

    private static void ValidateAttackType(PhysicalAttackType attackType)
    {
        if (attackType is not (PhysicalAttackType.Punch or PhysicalAttackType.Kick))
            throw new NotSupportedException(
                $"Physical attack resolution is not supported for attack type: {attackType}");
    }

    private static void ValidateLimb(PhysicalAttackType attackType, PartLocation limb)
    {
        switch (attackType)
        {
            case PhysicalAttackType.Punch when limb is not (PartLocation.LeftArm or PartLocation.RightArm):
                throw new ArgumentException(
                    $"A punch has to be made with an arm, but {limb} was used", nameof(limb));
            case PhysicalAttackType.Kick when limb is not (PartLocation.LeftLeg or PartLocation.RightLeg):
                throw new ArgumentException(
                    $"A kick has to be made with a leg, but {limb} was used", nameof(limb));
        }
    }
}
