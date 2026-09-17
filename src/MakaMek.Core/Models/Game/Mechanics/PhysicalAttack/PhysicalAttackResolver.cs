using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Resolves the initial simplified BattleMech punch and kick rules.
/// </summary>
public sealed class PhysicalAttackResolver : IPhysicalAttackResolver
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IDiceRoller _diceRoller;
    private readonly IDamageTransferCalculator _damageTransferCalculator;

    /// <summary>Initializes the resolver with shared game-rule services.</summary>
    public PhysicalAttackResolver(
        IRulesProvider rulesProvider,
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator)
    {
        _rulesProvider = rulesProvider;
        _diceRoller = diceRoller;
        _damageTransferCalculator = damageTransferCalculator;
    }

    /// <inheritdoc />
    public AttackResolutionData Resolve(IUnit attacker, IUnit target, PhysicalAttackType attackType)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        var attackRoll = _diceRoller.Roll2D6();
        var toHitNumber = attacker.Pilot?.Piloting ?? 5;
        var isHit = attackRoll.Sum(die => die.Result) >= toHitNumber;
        var attackDirection = DetermineAttackDirection(attacker, target);

        if (!isHit)
            return new AttackResolutionData(toHitNumber, attackRoll, false, attackDirection, 0);

        var locationRoll = _diceRoller.Roll2D6();
        var initialLocation = _rulesProvider.GetHitLocation(locationRoll.Sum(die => die.Result), attackDirection);
        var damage = Math.Max(1, attackType == PhysicalAttackType.Kick
            ? attacker.Tonnage / 5
            : attacker.Tonnage / 10);
        var damageData = _damageTransferCalculator.CalculateStructureDamage(
            target,
            initialLocation,
            damage,
            attackDirection);
        var hitLocation = new LocationHitData(
            damageData,
            [],
            locationRoll.Select(die => die.Result).ToArray(),
            initialLocation);

        return new AttackResolutionData(
            toHitNumber,
            attackRoll,
            true,
            attackDirection,
            0,
            new AttackHitLocationsData([hitLocation], damage, [], 1));
    }

    private static HitDirection DetermineAttackDirection(IUnit attacker, IUnit target)
    {
        if (attacker.Position == null || target.Position == null)
            return HitDirection.Front;

        return target.Position.Coordinates.GetFiringArc(
                attacker.Position.Coordinates,
                target.Position.Facing) switch
        {
            FiringArc.Left => HitDirection.Left,
            FiringArc.Right => HitDirection.Right,
            FiringArc.Rear => HitDirection.Rear,
            _ => HitDirection.Front
        };
    }
}
