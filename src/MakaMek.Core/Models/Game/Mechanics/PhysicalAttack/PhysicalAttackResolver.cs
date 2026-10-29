using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Pure mechanics resolution of a punch, a kick or a push. The resolver follows the resolution
/// order of the weapon resolver: the to-hit number comes from the physical attack calculator, the
/// hit is rolled on 2D6, the direction of the attack is taken from the target's facing, the hit
/// location is rolled on 1D6 on the punch or kick table and the damage is distributed by the
/// standard damage transfer calculator. A push deals no damage and has no hit location: it keeps
/// only the roll, the direction and the piloting skill requirement.
/// The resolver never mutates a unit: applying the damage and rolling the critical hits stays
/// with the caller, exactly as with weapon hits.
/// <para>
/// Rules ownership is split three ways: the supported attack catalog describes which limbs can
/// make an attack, the rules provider owns the damage reduction, hit location and piloting skill
/// roll obligations, and this resolver only performs component discovery and dice rolls.
/// </para>
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

        var definition = PhysicalAttackDefinitions.Get(context.AttackType);
        ValidateLimb(definition, context.AttackerLimb);

        if (context.Attacker.Position is null)
            throw new InvalidOperationException("Attacker position is not set");
        if (context.Target.Position is null)
            throw new InvalidOperationException("Target position is not set");

        var limbComponents = context.Attacker
            .GetComponentsAtLocation(context.AttackerLimb)
            .Select(component => new LimbComponentState(component.ComponentType, component.IsDestroyed))
            .ToList();
        var reducingActuators = _rulesProvider.GetPhysicalAttackReducingActuators(
            context.AttackType,
            limbComponents);
        var damage = _rulesProvider.GetPhysicalAttackDamage(
            context.Attacker.Tonnage,
            context.AttackType,
            reducingActuators);

        var toHitNumber = _toHitCalculator.GetToHitNumber(
            context.Attacker,
            context.Target,
            context.AttackType,
            context.Map);

        var attackRoll = _diceRoller.Roll2D6();
        var isHit = attackRoll.Sum(d => d.Result) >= toHitNumber;
        var psrRequirements = _rulesProvider.GetPhysicalAttackPsrRequirements(
            context.AttackType,
            isHit);

        if (!isHit)
        {
            return new PhysicalAttackResolutionData(
                context.AttackType,
                context.AttackerLimb,
                damage,
                CreateAttackResolution(toHitNumber, attackRoll, isHit: false, HitDirection.Front, null),
                psrRequirements);
        }

        var attackDirection = context.Target.GetAttackDirection(context.Attacker);

        // A push deals no damage and has no hit-location table: the displacement, not damage,
        // is its outcome. Keep the to-hit roll, direction and PSR requirement.
        if (context.AttackType == PhysicalAttackType.Push)
        {
            return new PhysicalAttackResolutionData(
                context.AttackType,
                context.AttackerLimb,
                damage,
                CreateAttackResolution(toHitNumber, attackRoll, isHit: true, attackDirection, null),
                psrRequirements);
        }

        var locationRoll = _diceRoller.RollD6();
        var rolledLocation = _rulesProvider.GetPhysicalAttackHitLocation(
            context.AttackType,
            locationRoll.Result,
            attackDirection);
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
            CreateAttackResolution(toHitNumber, attackRoll, isHit: true, attackDirection, hitLocationsData),
            psrRequirements);
    }

    /// <summary>
    /// Builds the weapon-shaped payload that the standard damage pipeline consumes. A physical
    /// attack generates no heat, has no cluster roll or missiles, and a miss is always reported as
    /// coming from the front. Isolating the adapter here documents the boundary instead of
    /// scattering weapon-only values through the resolver.
    /// </summary>
    private static AttackResolutionData CreateAttackResolution(
        int toHitNumber,
        List<DiceResult> attackRoll,
        bool isHit,
        HitDirection attackDirection,
        AttackHitLocationsData? hitLocationsData) =>
        new(
            toHitNumber,
            attackRoll,
            isHit,
            attackDirection,
            ExternalHeat: 0,
            HitLocationsData: hitLocationsData);

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

    private static void ValidateLimb(PhysicalAttackDefinition definition, PartLocation limb)
    {
        if (definition.CandidateLimbs.Contains(limb)) return;

        throw new ArgumentException(
            definition.AttackType switch
            {
                PhysicalAttackType.Punch => $"A punch has to be made with an arm, but {limb} was used",
                PhysicalAttackType.Kick => $"A kick has to be made with a leg, but {limb} was used",
                _ => $"A {definition.AttackType} has to be made with one of " +
                     $"{string.Join(", ", definition.CandidateLimbs)}, but {limb} was used"
            },
            nameof(limb));
    }
}
