using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Classic BattleTech implementation of to-hit calculator using the GATOR system
/// </summary>
public class ToHitCalculator : IToHitCalculator
{
    private readonly IRulesProvider _rules;

    public ToHitCalculator(IRulesProvider rules)
    {
        _rules = rules;
    }

    public int GetToHitNumber(
        IUnit attacker,
        IUnit target,
        Weapon weapon,
        IBattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null)
    {
        var breakdown = GetModifierBreakdown(attacker, target, weapon, map, isPrimaryTarget, aimedShotTarget);
        return breakdown.Total;
    }

    public ToHitBreakdown GetModifierBreakdown(
        IUnit attacker,
        IUnit target,
        Weapon weapon,
        IBattleMap map,
        bool isPrimaryTarget = true,
        PartLocation? aimedShotTarget = null)
    {
        var weaponLocation = weapon.FirstMountPartLocation ??
            throw new Exception($"Weapon {weapon.Name} is not mounted");
        var scenario = AttackScenario.FromUnits(attacker, target, weaponLocation, isPrimaryTarget, aimedShotTarget);
        return GetModifierBreakdown(scenario, weapon, map);
    }

    public int GetToHitNumber(AttackScenario scenario, Weapon weapon, IBattleMap map)
    {
        var breakdown = GetModifierBreakdown(scenario, weapon, map);
        return breakdown.Total;
    }

    public ToHitBreakdown GetModifierBreakdown(AttackScenario scenario, Weapon weapon, IBattleMap map)
    {
        var hasLos = map.HasLineOfSight(scenario.AttackerPosition.Coordinates, scenario.TargetPosition.Coordinates);
        var arc = GetFiringArc(scenario, weapon);
        var distance = scenario.AttackerPosition.Coordinates.DistanceTo(scenario.TargetPosition.Coordinates);
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

        var otherModifiers = GetDetailedOtherModifiers(scenario);
        var terrainModifiers = GetTerrainModifiers(
            scenario.AttackerPosition.Coordinates,
            scenario.TargetPosition.Coordinates,
            map);

        return new ToHitBreakdown
        {
            GunneryBase = new GunneryRollModifier
            {
                Value = scenario.AttackerGunnery
            },
            AttackerMovement = new AttackerMovementModifier
            {
                Value = _rules.GetAttackerMovementModifier(scenario.AttackerMovementType),
                MovementType = scenario.AttackerMovementType
            },
            TargetMovement = new TargetMovementModifier
            {
                Value = _rules.GetTargetMovementModifier(scenario.TargetHexesMoved),
                HexesMoved = scenario.TargetHexesMoved
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
            HasLineOfSight = hasLos,
            FiringArc = arc
        };
    }

    private FiringArc? GetFiringArc(AttackScenario scenario, Weapon weapon)
    {
        var arcs = weapon.GetFiringArcs();
        var facing = weapon.FirstMountPart?.Facing;
        if (facing == null)
            return null;
        foreach (var arc in arcs)
        {
            if (scenario.AttackerPosition.Coordinates.IsInFiringArc(
                scenario.TargetPosition.Coordinates,
                facing.Value,
                arc))
            {
                return arc;
            }
        }

        return null;
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

    private IReadOnlyList<RollModifier> GetDetailedOtherModifiers(AttackScenario scenario)
    {
        List<RollModifier> modifiers = [];

        // Add unit-specific modifiers from the scenario (heat, prone, sensors, arm actuators, etc.)
        modifiers.AddRange(scenario.AttackerModifiers);

        // Add an aimed shot modifier if applicable
        if (scenario.AimedShotTarget.HasValue)
        {
            modifiers.Add(new AimedShotModifier
            {
                TargetLocation = scenario.AimedShotTarget.Value,
                Value = _rules.GetAimedShotModifier(scenario.AimedShotTarget.Value)
            });
        }

        // Add a secondary target modifier if not primary
        if (scenario is not { IsPrimaryTarget: false, AttackerFacing: not null }) return modifiers;
        var isInFrontArc = scenario.AttackerPosition.Coordinates.IsInFiringArc(
            scenario.TargetPosition.Coordinates,
            scenario.AttackerFacing.Value,
            FiringArc.Front);

        modifiers.Add(new SecondaryTargetModifier
        {
            IsInFrontArc = isInFrontArc,
            Value = _rules.GetSecondaryTargetModifier(isInFrontArc)
        });

        return modifiers;
    }

    private IReadOnlyList<TerrainRollModifier> GetTerrainModifiers(HexCoordinates attackerPosition, HexCoordinates targetPosition, IBattleMap map)
    {
        var hexes = map.GetHexesAlongLineOfSight(attackerPosition, targetPosition);

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
