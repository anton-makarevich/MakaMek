using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;

public class WeaponAttackResolver : IWeaponAttackResolver
{
    private readonly IRulesProvider _rulesProvider;
    private readonly IDiceRoller _diceRoller;
    private readonly IDamageTransferCalculator _damageTransferCalculator;
    private readonly IToHitCalculator _toHitCalculator;

    public WeaponAttackResolver(
        IRulesProvider rulesProvider,
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator,
        IToHitCalculator toHitCalculator)
    {
        _rulesProvider = rulesProvider;
        _diceRoller = diceRoller;
        _damageTransferCalculator = damageTransferCalculator;
        _toHitCalculator = toHitCalculator;
    }

    public AttackResolutionData ResolveAttack(
        IUnit attacker,
        IUnit target,
        Weapon weapon,
        WeaponTargetData weaponTargetData,
        IBattleMap battleMap)
    {
        ArgumentNullException.ThrowIfNull(battleMap);

        if (weapon.FirstMountPart == null)
        {
            throw new ArgumentException($"Weapon {weapon.Name} is not mounted", nameof(weapon));
        }

        var toHitNumber = _toHitCalculator.GetToHitNumber(
            attacker,
            target,
            weapon,
            battleMap,
            weaponTargetData.IsPrimaryTarget,
            weaponTargetData.AimedShotTarget);

        var attackRoll = _diceRoller.Roll2D6();
        var totalRoll = attackRoll.Sum(d => d.Result);

        var isHit = totalRoll >= toHitNumber;

        var attackDirection = HitDirection.Front;

        AttackHitLocationsData? hitLocationsData = null;

        if (!isHit) return new AttackResolutionData(toHitNumber,
            attackRoll,
            isHit,
            attackDirection,
            weapon.ExternalHeat,
            hitLocationsData);

        attackDirection = DetermineAttackDirection(attacker, target);

        var losResult = battleMap.GetLineOfSight(
            attacker.Position!.Coordinates,
            target.Position!.Coordinates,
            weapon.FirstMountPart.Level,
            target.Height,
            attacker.Position.Surface,
            target.Position.Surface);
        var hasPartialCover = _rulesProvider.HasPartialCover(target, losResult);
        var coveringHex = hasPartialCover && losResult.HexPath.Count >= 2
            ? losResult.HexPath[^2].Hex.Coordinates.ToData()
            : null;

        if (weapon.WeaponSize > 1)
        {
            hitLocationsData = ResolveClusterWeaponHit(weapon, target, attackDirection, weaponTargetData, hasPartialCover, coveringHex);

            return new AttackResolutionData(toHitNumber,
                attackRoll,
                isHit,
                attackDirection,
                weapon.ExternalHeat,
                hitLocationsData);
        }

        var hitLocationData = DetermineHitLocation(attackDirection, weapon.Damage, target, weapon, weaponTargetData, null, hasPartialCover, coveringHex);

        hitLocationsData = new AttackHitLocationsData(
            [hitLocationData],
            weapon.Damage,
            [],
            1
        );

        return new AttackResolutionData(toHitNumber,
            attackRoll,
            isHit,
            attackDirection,
            weapon.ExternalHeat,
            hitLocationsData);
    }

    private AttackHitLocationsData ResolveClusterWeaponHit(Weapon weapon,
        IUnit target,
        HitDirection attackDirection,
        WeaponTargetData weaponTargetData,
        bool hasPartialCover = false,
        HexCoordinateData? coveringHex = null)
    {
        var clusterRoll = _diceRoller.Roll2D6();
        var clusterRollTotal = clusterRoll.Sum(d => d.Result);

        var missilesHit = _rulesProvider.GetClusterHits(clusterRollTotal, weapon.WeaponSize);

        var damagePerMissile = weapon.Damage / weapon.WeaponSize;

        var completeClusterHits = missilesHit / weapon.ClusterSize;
        var remainingMissiles = missilesHit % weapon.ClusterSize;

        var hitLocations = new List<LocationHitData>();
        var totalDamage = 0;

        for (var i = 0; i < completeClusterHits; i++)
        {
            var clusterDamage = weapon.ClusterSize * damagePerMissile;

            var hitLocationData = DetermineHitLocation(attackDirection,
                clusterDamage,
                target,
                weapon,
                weaponTargetData,
                hitLocations,
                hasPartialCover,
                coveringHex);

            hitLocations.Add(hitLocationData);
            totalDamage += clusterDamage;
        }

        if (remainingMissiles > 0)
        {
            var partialClusterDamage = remainingMissiles * damagePerMissile;

            var hitLocationData = DetermineHitLocation(
                attackDirection,
                partialClusterDamage,
                target,
                weapon,
                weaponTargetData,
                hitLocations,
                hasPartialCover,
                coveringHex);

            hitLocations.Add(hitLocationData);
            totalDamage += partialClusterDamage;
        }

        return new AttackHitLocationsData(hitLocations, totalDamage, clusterRoll, missilesHit);
    }

    private LocationHitData DetermineHitLocation(
        HitDirection attackDirection,
        int damage,
        IUnit target,
        Weapon weapon,
        WeaponTargetData weaponTargetData,
        IReadOnlyList<LocationHitData>? accumulatedHitLocations = null,
        bool hasPartialCover = false,
        HexCoordinateData? coveringHex = null)
    {
        PartLocation? aimedShotLocation = null;
        int[] aimedShotRollResult = [];
        if (IsAimedShotPossible())
        {
            aimedShotRollResult = _diceRoller.Roll2D6().Select(d => d.Result).ToArray();
            var aimedShotRoll = aimedShotRollResult.Sum();
            var successValues = _rulesProvider.GetAimedShotSuccessValues();
            if (successValues.Contains(aimedShotRoll))
            {
                aimedShotLocation = weaponTargetData.AimedShotTarget;
            }
        }

        int[] locationRoll = [];
        var hitLocation = aimedShotLocation ?? GetHitLocation(out locationRoll);

        if (hasPartialCover && coveringHex != null &&
            _rulesProvider.CanPartBeCovered(hitLocation))
        {
            return new LocationHitData(
                [],
                aimedShotRollResult,
                locationRoll,
                hitLocation,
                new CoveringHexData(coveringHex, damage));
        }

        var initialLocation = hitLocation;
        var visited = new HashSet<PartLocation> { hitLocation };

        while (target.Parts.TryGetValue(hitLocation, out var part) && part.IsDestroyed)
        {
            var nextLocation = part.GetNextTransferLocation();
            if (nextLocation == null || !visited.Add(nextLocation.Value))
                break;

            hitLocation = nextLocation.Value;
        }

        var damageData = _damageTransferCalculator.CalculateStructureDamage(
            target, hitLocation, damage, attackDirection, accumulatedHitLocations);

        return new LocationHitData(
            damageData,
            aimedShotRollResult,
            locationRoll,
            initialLocation);

        bool IsAimedShotPossible()
        {
            return target.IsImmobile
                   && weapon.IsAimShotCapable
                   && weaponTargetData.AimedShotTarget.HasValue;
        }

        PartLocation GetHitLocation(out int[] innerLocationRoll)
        {
            innerLocationRoll = _diceRoller.Roll2D6().Select(d => d.Result).ToArray();
            var locationRollTotal = innerLocationRoll.Sum();
            return _rulesProvider.GetHitLocation(locationRollTotal, attackDirection);
        }
    }

    private HitDirection DetermineAttackDirection(IUnit? attacker, IUnit target)
    {
        if (attacker?.Position == null || target.Position == null)
            return HitDirection.Front;

        var arc = target.Position.Coordinates.GetFiringArc(
            attacker.Position.Coordinates, target.Position.Facing);

        return arc switch
        {
            FiringArc.Left => HitDirection.Left,
            FiringArc.Right => HitDirection.Right,
            FiringArc.Rear => HitDirection.Rear,
            _ => HitDirection.Front
        };
    }
}