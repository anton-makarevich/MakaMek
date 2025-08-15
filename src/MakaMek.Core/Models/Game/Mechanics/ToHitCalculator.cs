using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Classic BattleTech implementation of to-hit calculator using GATOR system
/// </summary>
public class ToHitCalculator : IToHitCalculator
{
    private readonly IRulesProvider _rules;

    public ToHitCalculator(IRulesProvider rules)
    {
        _rules = rules;
    }

    public int GetToHitNumber(
        Unit attacker,
        Unit target,
        Weapon weapon,
        BattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null)
    {
        var breakdown = GetModifierBreakdown(attacker, target, weapon, map, isPrimaryTarget, aimedShotTarget);
        return breakdown.Total;
    }

    public ToHitBreakdown GetModifierBreakdown(
        Unit attacker,
        Unit target,
        Weapon weapon,
        BattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null)
    {
        if (attacker.Pilot is null)
        {
            throw new Exception("Attacker pilot is not assigned");
        }
        var hasLos = map.HasLineOfSight(attacker.Position!.Coordinates, target.Position!.Coordinates);
        var distance = attacker.Position!.Coordinates.DistanceTo(target.Position!.Coordinates);
        var range = weapon.GetRangeBracket(distance);
        var rangeValue = range switch
        {
            WeaponRange.Minimum => weapon.MinimumRange,
            WeaponRange.Short => weapon.ShortRange,
            WeaponRange.Medium => weapon.MediumRange,
            WeaponRange.Long => weapon.LongRange,
            WeaponRange.OutOfRange => weapon.LongRange+1,
            _ => throw new ArgumentException($"Unknown weapon range: {range}")
        };
        var weaponLocation = weapon.MountedOn?.Location ?? throw new Exception($"Weapon {weapon.Name} is not mounted");
        var otherModifiers = GetDetailedOtherModifiers(attacker, target,weaponLocation, isPrimaryTarget, aimedShotTarget);
        var terrainModifiers = GetTerrainModifiers(attacker, target, map);

        return new ToHitBreakdown
        {
            GunneryBase = new GunneryRollModifier
            {
                Value = attacker.Pilot.Gunnery
            },
            AttackerMovement = new AttackerMovementModifier
            {
                Value = _rules.GetAttackerMovementModifier(attacker.MovementTypeUsed ??
                    throw new Exception("Attacker's Movement Type is undefined")),
                MovementType = attacker.MovementTypeUsed.Value
            },
            TargetMovement = new TargetMovementModifier
            {
                Value = _rules.GetTargetMovementModifier(target.DistanceCovered),
                HexesMoved = target.DistanceCovered
            },
            OtherModifiers = otherModifiers,
            RangeModifier = new RangeRollModifier
            {
                Value = _rules.GetRangeModifier(range, rangeValue, distance),
                Range = range,
                Distance = distance,
                WeaponName = weapon.Name
            },
            TerrainModifiers = terrainModifiers,
            HasLineOfSight = hasLos
        };
    }

    /// <summary>
    /// Creates a new ToHitBreakdown by adding or replacing the aimed shot modifier in the existing breakdown.
    /// This is more efficient than recalculating the entire breakdown when only the aimed shot target changes.
    /// </summary>
    /// <param name="existingBreakdown">The existing breakdown to modify</param>
    /// <param name="aimedShotTarget">The body part being targeted for the aimed shot</param>
    /// <returns>A new ToHitBreakdown with the aimed shot modifier added</returns>
    public ToHitBreakdown AddAimedShotModifier(ToHitBreakdown existingBreakdown, PartLocation aimedShotTarget)
    {
        // Create the aimed shot modifier for the target location
        var aimedShotModifier = new AimedShotModifier
        {
            TargetLocation = aimedShotTarget,
            Value = _rules.GetAimedShotModifier(aimedShotTarget)
        };

        // Create a new list with the existing modifiers plus the aimed shot modifier
        var newOtherModifiers = existingBreakdown.OtherModifiers
            .Where(m => m is not AimedShotModifier) // Remove any existing aimed shot modifier
            .Append(aimedShotModifier)
            .ToList();

        // Return a new breakdown with only the OtherModifiers changed
        return existingBreakdown with
        {
            OtherModifiers = newOtherModifiers
        };
    }

    private IReadOnlyList<RollModifier> GetDetailedOtherModifiers(Unit attacker, Unit target, PartLocation weaponLocation, bool isPrimaryTarget = true, PartLocation? aimedShotTarget = null)
    {
        List<RollModifier> modifiers = [];
        // Unit specific modifiers
        // Depend on the unit type
        modifiers.AddRange(attacker.GetAttackModifiers(weaponLocation));

        // Add aimed shot modifier if applicable
        if (aimedShotTarget.HasValue)
        {
            modifiers.Add(new AimedShotModifier
            {
                TargetLocation = aimedShotTarget.Value,
                Value = _rules.GetAimedShotModifier(aimedShotTarget.Value)
            });
        }

        // Add secondary target modifier if not primary
        if (!isPrimaryTarget && attacker is { Position: not null } && target is { Position: not null })
        {
            var attackerPosition = attacker.Position;
            var facing = attacker is Mech mech ? mech.TorsoDirection : attackerPosition.Facing;

            if (facing != null)
            {
                var isInFrontArc = attackerPosition.Coordinates.IsInFiringArc(
                    target.Position.Coordinates,
                    facing.Value,
                    FiringArc.Forward);

                modifiers.Add(new SecondaryTargetModifier
                {
                    IsInFrontArc = isInFrontArc,
                    Value = _rules.GetSecondaryTargetModifier(isInFrontArc)
                });
            }
        }

        return modifiers;
    }

    private IReadOnlyList<TerrainRollModifier> GetTerrainModifiers(Unit attacker, Unit target, BattleMap map)
    {
        var hexes = map.GetHexesAlongLineOfSight(
            attacker.Position!.Coordinates,
            target.Position!.Coordinates);

        return hexes
            .Skip(1) // Skip attacker's hex
            .SelectMany(hex => hex.GetTerrains()
                .Select(terrain => new TerrainRollModifier
                {
                    Value = _rules.GetTerrainToHitModifier(terrain.Id),
                    Location = hex.Coordinates,
                    TerrainId = terrain.Id
                }))
            .Where(modifier => modifier.Value != 0)
            .ToList();
    }
}
