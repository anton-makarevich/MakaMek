using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Rules;

public class TotalWarfareRulesProvider : IRulesProvider
{
    // Head, CenterTorso, LeftTorso, RightTorso, LeftArm, RightArm, LeftLeg, RightLeg
    private static readonly Dictionary<int, int[]> StructureValuesByTonnage = new()
    {
        [20] = [3, 6, 5, 5, 3, 3, 4, 4],
        [25] = [3, 8, 6, 6, 4, 4, 6, 6],
        [30] = [3, 10, 7, 7, 5, 5, 7, 7],
        [35] = [3, 11, 8, 8, 6, 6, 8, 8],
        [40] = [3, 12, 10, 10, 6, 6, 10, 10],
        [45] = [3, 14, 11, 11, 7, 7, 11, 11],
        [50] = [3, 16, 12, 12, 8, 8, 12, 12],
        [55] = [3, 18, 13, 13, 9, 9, 13, 13],
        [60] = [3, 20, 14, 14, 10, 10, 14, 14],
        [65] = [3, 21, 15, 15, 10, 10, 15, 15],
        [70] = [3, 22, 15, 15, 11, 11, 15, 15],
        [75] = [3, 23, 16, 16, 12, 12, 16, 16],
        [80] = [3, 25, 17, 17, 13, 13, 17, 17],
        [85] = [3, 27, 18, 18, 14, 14, 18, 18],
        [90] = [3, 29, 19, 19, 15, 15, 19, 19],
        [95] = [3, 30, 20, 20, 16, 16, 20, 20],
        [100] = [3, 31, 21, 21, 17, 17, 21, 21]
    };

    private static readonly PartLocation[] StructureLocations =
    [
        PartLocation.Head,
        PartLocation.CenterTorso,
        PartLocation.LeftTorso,
        PartLocation.RightTorso,
        PartLocation.LeftArm,
        PartLocation.RightArm,
        PartLocation.LeftLeg,
        PartLocation.RightLeg
    ];

    public Dictionary<PartLocation, int> GetStructureValues(int tonnage)
    {
        if (!StructureValuesByTonnage.TryGetValue(tonnage, out var values))
        {
            throw new ArgumentOutOfRangeException(nameof(tonnage), "Invalid tonnage");
        }

        var structureValues = new Dictionary<PartLocation, int>(StructureLocations.Length);
        for (var i = 0; i < StructureLocations.Length; i++)
        {
            structureValues[StructureLocations[i]] = values[i];
        }

        return structureValues;
    }

    public int GetAttackerMovementModifier(MovementType movementType)
    {
        return movementType switch
        {
            MovementType.StandingStill => 0,
            MovementType.Walk => 1,
            MovementType.Run => 2,
            MovementType.Jump => 3,
            MovementType.Prone => 2,
            _ => throw new ArgumentException($"Unknown movement type: {movementType}")
        };
    }

    public int GetTargetMovementModifier(int hexesMoved)
    {
        return hexesMoved switch
        {
            <= 2 => 0, // 0-2 hexes: no modifier
            <= 4 => 1, // 3-4 hexes: +1
            <= 6 => 2, // 5-6 hexes: +2
            <= 9 => 3, // 7-9 hexes: +3
            <= 17 => 4, // 10-17 hexes: +4
            <= 24 => 5, // 18-24 hexes: +5
            _ => 6 // 25+ hexes: +6
        };
    }

    public int GetDfaAirborneBaseOffset() => 2;

    /// <summary>
    /// Gets the per-type base modifier for a physical attack. Punch, Kick, Push, Charge and DFA are
    /// supported; other types throw. Later milestones extend the table: physical weapons -1 or -2
    /// (M4), Club -1 (M5).
    /// </summary>
    public int GetPhysicalAttackBaseModifier(PhysicalAttackType attackType)
    {
        return attackType switch
        {
            PhysicalAttackType.Punch => 0,
            PhysicalAttackType.Kick => -2,
            PhysicalAttackType.Push => -1,
            PhysicalAttackType.Charge => 0,
            PhysicalAttackType.DFA => 0,
            _ => throw new NotSupportedException(
                $"Physical attack base modifier is not supported for attack type: {attackType}")
        };
    }

    /// <inheritdoc />
    public int GetPushDamagedShoulderModifier() => 2;

    /// <summary>
    /// Physical attack damage is based on the attacker's tonnage: one point per ten tons for a
    /// punch and one point per five tons for a kick, rounded up. A push deals no damage. Each
    /// damaged or missing actuator halves the result, rounding down to a minimum of one point
    /// (Total Warfare, p. 145-148).
    /// </summary>
    public int GetPhysicalAttackDamage(int tonnage, PhysicalAttackType attackType, int reducingActuators)
    {
        if (attackType == PhysicalAttackType.Push) return 0;

        var divisor = attackType switch
        {
            PhysicalAttackType.Punch => 10,
            PhysicalAttackType.Kick => 5,
            _ => throw new NotSupportedException(
                $"Physical attack damage is not supported for attack type: {attackType}")
        };

        var damage = (tonnage + divisor - 1) / divisor;
        for (var i = 0; i < reducingActuators; i++)
            damage = Math.Max(1, damage / 2);

        return damage;
    }

    /// <inheritdoc />
    public int GetChargeDamage(int attackerTonnage, int hexesMoved)
    {
        if (attackerTonnage < 0)
            throw new ArgumentOutOfRangeException(nameof(attackerTonnage), "Tonnage cannot be negative.");
        if (hexesMoved < 0)
            throw new ArgumentOutOfRangeException(nameof(hexesMoved), "Hexes moved cannot be negative.");

        // Multiplication precedes rounding: (tonnage / 10) × hexes, rounded up.
        return (attackerTonnage * hexesMoved + 9) / 10;
    }

    /// <inheritdoc />
    public int GetChargeCollisionDamage(int targetTonnage)
    {
        if (targetTonnage < 0)
            throw new ArgumentOutOfRangeException(nameof(targetTonnage), "Tonnage cannot be negative.");

        return (targetTonnage + 9) / 10;
    }

    /// <inheritdoc />
    public int GetDfaDamage(int attackerTonnage)
    {
        if (attackerTonnage < 0)
            throw new ArgumentOutOfRangeException(nameof(attackerTonnage), "Tonnage cannot be negative.");

        // Three times the tonnage, divided by ten, rounding the final result up: a 35-ton Mech
        // inflicts 11 points, not 12 (Total Warfare, p. 150).
        return (attackerTonnage * 3 + 9) / 10;
    }

    /// <inheritdoc />
    public int GetDfaAttackerDamage(int attackerTonnage)
    {
        if (attackerTonnage < 0)
            throw new ArgumentOutOfRangeException(nameof(attackerTonnage), "Tonnage cannot be negative.");

        return (attackerTonnage + 4) / 5;
    }

    /// <summary>
    /// A kick forces the target to make a piloting skill roll when it hits and the attacker to
    /// make one when it misses (Total Warfare, p. 146). A push forces the target to roll when it
    /// hits (p. 148). A punch creates no obligation of its own.
    /// Requirements disabled through <see cref="RequiresPilotingSkillRoll"/> are omitted.
    /// </summary>
    public IReadOnlyList<PhysicalAttackPsrRequirement> GetPhysicalAttackPsrRequirements(
        PhysicalAttackType attackType,
        bool isHit)
    {
        IReadOnlyList<PhysicalAttackPsrRequirement> candidates = attackType switch
        {
            PhysicalAttackType.Punch => [],
            PhysicalAttackType.Push when isHit =>
                [new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Target, PilotingSkillRollType.PushHit)],
            PhysicalAttackType.Push => [],
            PhysicalAttackType.Kick when isHit =>
                [new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Target, PilotingSkillRollType.KickHit)],
            PhysicalAttackType.Kick =>
                [new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Attacker, PilotingSkillRollType.KickMiss)],
            PhysicalAttackType.Charge when isHit =>
            [
                new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Attacker, PilotingSkillRollType.ChargeImpact),
                new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Target, PilotingSkillRollType.ChargeImpact)
            ],
            PhysicalAttackType.Charge => [],
            // A successful DFA makes the target roll at +2 and the attacker at +4. A miss makes
            // the attacker fall automatically, so it rolls no PSR of its own (Total Warfare, p. 150).
            PhysicalAttackType.DFA when isHit =>
            [
                new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Target, PilotingSkillRollType.DfaTargetImpact),
                new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Attacker, PilotingSkillRollType.DfaAttackerImpact)
            ],
            PhysicalAttackType.DFA => [],
            _ => throw new NotSupportedException(
                $"Physical attack piloting skill roll requirements are not supported for attack type: {attackType}")
        };

        return candidates
            .Where(requirement => RequiresPilotingSkillRoll(requirement.RollType))
            .ToList();
    }

    /// <summary>
    /// A punch is halved by each damaged or missing upper or lower arm actuator of the punching
    /// arm; a kick by each destroyed upper or lower leg actuator of the kicking leg
    /// (Total Warfare, p. 146-148).
    /// </summary>
    public int GetPhysicalAttackReducingActuators(
        PhysicalAttackType attackType,
        IReadOnlyCollection<LimbComponentState> limbComponents)
    {
        return attackType switch
        {
            PhysicalAttackType.Punch =>
                CountDamagedOrMissing(limbComponents, MakaMekComponent.UpperArmActuator)
                + CountDamagedOrMissing(limbComponents, MakaMekComponent.LowerArmActuator),
            PhysicalAttackType.Kick =>
                CountDestroyed(limbComponents, MakaMekComponent.UpperLegActuator)
                + CountDestroyed(limbComponents, MakaMekComponent.LowerLegActuator),
            // A push deals no damage, so no actuator can reduce it.
            PhysicalAttackType.Push => 0,
            _ => throw new NotSupportedException(
                $"Physical attack actuator damage reduction is not supported for attack type: {attackType}")
        };
    }

    private static int CountDestroyed(
        IReadOnlyCollection<LimbComponentState> limbComponents,
        MakaMekComponent type) =>
        limbComponents.Count(component => component.Type == type && component.IsDestroyed);

    private static int CountDamagedOrMissing(
        IReadOnlyCollection<LimbComponentState> limbComponents,
        MakaMekComponent type)
    {
        var matching = limbComponents.Where(component => component.Type == type).ToList();
        return matching.Count == 0 ? 1 : matching.Count(component => component.IsDestroyed);
    }

    /// <summary>
    /// Dispatches a physical attack hit-location roll to the punch or kick table.
    /// </summary>
    public PartLocation GetPhysicalAttackHitLocation(
        PhysicalAttackType attackType,
        int roll,
        HitDirection direction) =>
        attackType switch
        {
            PhysicalAttackType.Punch => GetPunchHitLocation(roll, direction),
            PhysicalAttackType.Kick => GetKickHitLocation(roll, direction),
            // A charge uses the standard 2D6 hit location table, so the roll is already a 2D6 total.
            PhysicalAttackType.Charge => GetHitLocation(roll, direction),
            _ => throw new NotSupportedException(
                $"Physical attack hit location is not supported for attack type: {attackType}")
        };

    public int GetRangeModifier(RangeBracket rangeBracketType, int rangeValue, int distance)
    {
        return rangeBracketType switch
        {
            RangeBracket.Minimum => rangeValue - distance + 1,
            RangeBracket.Short => 0,
            RangeBracket.Medium => 2,
            RangeBracket.Long => 4,
            RangeBracket.OutOfRange => ToHitBreakdown.ImpossibleRoll,
            _ => throw new ArgumentException($"Unknown weapon rangeBracket: {rangeBracketType}")
        };
    }

    public int GetTerrainToHitModifier(MakaMekTerrains terrainId)
    {
        return terrainId switch
        {
            MakaMekTerrains.LightWoods => 1,
            MakaMekTerrains.HeavyWoods => 2,
            _ => 0 // Default no modifier
        };
    }

    public int GetSecondaryTargetModifier(bool isFrontArc)
    {
        return isFrontArc ? 1 : 2; // +1 for front arc, +2 for other arcs
    }

    /// <summary>
    /// 'Mech Punch Location Table (Total Warfare, p. 146 / p. 309). Rolled on 1D6. The column comes
    /// from where the attack enters the target's hex relative to the target's facing, so a
    /// side-arc punch that enters the front or rear rolls on the front/rear column.
    /// </summary>
    public PartLocation GetPunchHitLocation(int diceResult, HitDirection attackDirection)
    {
        if (diceResult is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(diceResult), "Invalid dice result");

        return attackDirection switch
        {
            HitDirection.Left => diceResult switch
            {
                1 or 2 => PartLocation.LeftTorso,
                3 => PartLocation.CenterTorso,
                4 or 5 => PartLocation.LeftArm,
                _ => PartLocation.Head
            },
            HitDirection.Right => diceResult switch
            {
                1 or 2 => PartLocation.RightTorso,
                3 => PartLocation.CenterTorso,
                4 or 5 => PartLocation.RightArm,
                _ => PartLocation.Head
            },
            _ => diceResult switch // Front or Rear
            {
                1 => PartLocation.LeftArm,
                2 => PartLocation.LeftTorso,
                3 => PartLocation.CenterTorso,
                4 => PartLocation.RightTorso,
                5 => PartLocation.RightArm,
                _ => PartLocation.Head
            }
        };
    }

    /// <summary>
    /// 'Mech Kick Location Table (Total Warfare, p. 148 / p. 309). Rolled on 1D6. A kick always
    /// strikes a leg. On the side columns the near leg is the one on the struck side, so every
    /// side-column roll hits that leg; the front/rear column reads right then left.
    /// </summary>
    public PartLocation GetKickHitLocation(int diceResult, HitDirection attackDirection)
    {
        if (diceResult is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(diceResult), "Invalid dice result");

        return attackDirection switch
        {
            HitDirection.Left => PartLocation.LeftLeg,
            HitDirection.Right => PartLocation.RightLeg,
            _ => diceResult <= 3 ? PartLocation.RightLeg : PartLocation.LeftLeg // Front or Rear
        };
    }

    public PartLocation GetHitLocation(int diceResult, HitDirection attackDirection)
    {
        // Classic BattleTech hit location tables based on the attack direction
        // Note: Front and Rear directions use the same hit location table
        return attackDirection switch
        {
            HitDirection.Front or HitDirection.Rear => diceResult switch
            {
                2 => PartLocation.CenterTorso, // Critical hit
                3 => PartLocation.RightArm,
                4 => PartLocation.RightArm,
                5 => PartLocation.RightLeg,
                6 => PartLocation.RightTorso,
                7 => PartLocation.CenterTorso,
                8 => PartLocation.LeftTorso,
                9 => PartLocation.LeftLeg,
                10 => PartLocation.LeftArm,
                11 => PartLocation.LeftArm,
                12 => PartLocation.Head,
                _ => throw new ArgumentOutOfRangeException(nameof(diceResult), "Invalid dice result")
            },
            HitDirection.Left => diceResult switch
            {
                2 => PartLocation.LeftTorso, // Critical hit
                3 => PartLocation.LeftLeg,
                4 => PartLocation.LeftArm,
                5 => PartLocation.LeftArm,
                6 => PartLocation.LeftLeg,
                7 => PartLocation.LeftTorso,
                8 => PartLocation.CenterTorso,
                9 => PartLocation.RightTorso,
                10 => PartLocation.RightArm,
                11 => PartLocation.RightLeg,
                12 => PartLocation.Head,
                _ => throw new ArgumentOutOfRangeException(nameof(diceResult), "Invalid dice result")
            },
            HitDirection.Right => diceResult switch
            {
                2 => PartLocation.RightTorso, // Critical hit
                3 => PartLocation.RightLeg,
                4 => PartLocation.RightArm,
                5 => PartLocation.RightArm,
                6 => PartLocation.RightLeg,
                7 => PartLocation.RightTorso,
                8 => PartLocation.CenterTorso,
                9 => PartLocation.LeftTorso,
                10 => PartLocation.LeftArm,
                11 => PartLocation.LeftLeg,
                12 => PartLocation.Head,
                _ => throw new ArgumentOutOfRangeException(nameof(diceResult), "Invalid dice result")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(attackDirection), "Invalid attack direction")
        };
    }

    public int GetClusterHits(int diceResult, int weaponSize)
    {
        // Implementation of the Cluster Hits Table
        // Returns the number of missiles that hit based on 2D6 roll and weapon size

        // Special case for non-cluster weapons
        if (weaponSize <= 1)
            return weaponSize;

        // First, determine which column to use based on weapon size
        int columnIndex;
        switch (weaponSize)
        {
            case 2: columnIndex = 0; break;
            case 4: columnIndex = 1; break;
            case 5: columnIndex = 2; break;
            case 6: columnIndex = 3; break;
            case 10: columnIndex = 4; break;
            case 15: columnIndex = 5; break;
            case 20: columnIndex = 6; break;
            default:
                // For unsupported weapon sizes, return the weapon size itself
                // This ensures we don't throw an exception for valid but non-standard weapon sizes
                return weaponSize;
        }

        // Define the cluster hits table
        // Format: [diceResult][columnIndex]
        var clusterHitsTable = new[,]
        {
            // 2,  4,  5,  6,  10, 15, 20 (weapon sizes)
            { 1, 1, 1, 2, 3, 5, 6 }, // Roll of 2
            { 1, 2, 2, 2, 3, 5, 6 }, // Roll of 3
            { 1, 2, 2, 3, 4, 6, 9 }, // Roll of 4
            { 1, 2, 3, 3, 6, 9, 12 }, // Roll of 5
            { 1, 2, 3, 4, 6, 9, 12 }, // Roll of 6
            { 1, 3, 3, 4, 6, 9, 12 }, // Roll of 7
            { 2, 3, 3, 4, 6, 9, 12 }, // Roll of 8
            { 2, 3, 4, 5, 8, 12, 16 }, // Roll of 9
            { 2, 3, 4, 5, 8, 12, 16 }, // Roll of 10
            { 2, 4, 5, 6, 10, 15, 20 }, // Roll of 11
            { 2, 4, 5, 6, 10, 15, 20 } // Roll of 12
        };

        // Adjust dice result to 0-based index (2 becomes 0, 3 becomes 1, etc.)
        var rowIndex = diceResult - 2;

        // Ensure the indices are within bounds
        if (rowIndex < 0 || rowIndex >= 11)
            throw new ArgumentOutOfRangeException(nameof(diceResult), "Dice result must be between 2 and 12");

        // Get the number of hits from the table
        var hits = clusterHitsTable[rowIndex, columnIndex];

        // Ensure we don't return more hits than the weapon size
        return Math.Min(hits, weaponSize);
    }

    public HexDirection GetFacingAfterFall(int roll, HexDirection currentFacing)
    {
        if (roll < 1 || roll > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(roll), "Roll must be between 1 and 6");
        }

        // Determine the new facing relative to current facing
        return currentFacing.Rotate(roll - 1);
    }

    public HitDirection GetAttackDirectionAfterFall(int roll)
    {
        // Determine the attack direction for hit location
        return roll switch
        {
            1 => HitDirection.Front, // Front
            2 => HitDirection.Right, // Right side
            3 => HitDirection.Right, // Right side
            4 => HitDirection.Rear, // Rear
            5 => HitDirection.Left, // Left side
            6 => HitDirection.Left, // Left side
            _ => throw new ArgumentOutOfRangeException(nameof(roll), "Roll must be between 1 and 6")
        };
    }

    /// <summary>
    /// Gets the heat points generated by movement based on movement type and points spent
    /// </summary>
    /// <param name="movementType">The type of movement used</param>
    /// <param name="movementPointSpent">The number of movement points spent</param>
    /// <returns>Heat points generated by movement</returns>
    public int GetMovementHeatPoints(MovementType movementType, int movementPointSpent)
    {
        return movementType switch
        {
            MovementType.Walk => 1,
            MovementType.Run => 2,
            MovementType.Jump => Math.Max(3, movementPointSpent),
            _ => 0 // No heat for other movement types
        };
    }

    public int GetPilotingSkillRollModifier(PilotingSkillRollType psrType)
    {
        return psrType switch
        {
            PilotingSkillRollType.GyroHit => 3,
            PilotingSkillRollType.GyroDestroyed => 6,
            PilotingSkillRollType.LowerLegActuatorHit => 1,
            PilotingSkillRollType.HeavyDamage => 1, // +1 modifier for taking 20+ damage in a single phase
            PilotingSkillRollType.HipActuatorHit => 2,
            PilotingSkillRollType.FootActuatorHit => 1,
            PilotingSkillRollType.UpperLegActuatorHit => 1,
            PilotingSkillRollType.LegDestroyed => 5, // +5 modifier for leg destroyed (pilot damage during fall)
            PilotingSkillRollType.BridgeCollapse => 0,
            PilotingSkillRollType.RubbleEntry => 0,
            // docs/rules/physical-attacks.md section 3 calls for both rolls and gives no modifier for either.
            PilotingSkillRollType.KickHit => 0,
            PilotingSkillRollType.KickMiss => 0,
            // A successful push gives the defender no extra modifier (unlike Charge/DFA, which
            // are +2): Total Warfare p. 148 asks for a plain Piloting Skill Roll.
            PilotingSkillRollType.PushHit => 0,
            // A successful charge makes both units roll at +2 (Total Warfare, p. 148).
            PilotingSkillRollType.ChargeImpact => 2,
            // A successful DFA makes the target roll at +2 and the attacker at +4 (Total Warfare,
            // p. 150).
            PilotingSkillRollType.DfaTargetImpact => 2,
            PilotingSkillRollType.DfaAttackerImpact => 4,
            // A domino effect adds no modifier of its own; the ordinary PSR modifiers apply
            // (Total Warfare, p. 151-153).
            PilotingSkillRollType.DominoEffect => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(psrType), "Invalid piloting skill roll type")
        };
    }

    // The default value is "PSR required". When adding new auto-fall PilotingSkillRollType values
    // (e.g., future Shutdown/Skid auto-fall variants), add them to the false-returning list below.
    public bool RequiresPilotingSkillRoll(PilotingSkillRollType rollType) => rollType switch
    {
        PilotingSkillRollType.GyroDestroyed => false,
        PilotingSkillRollType.LegDestroyed => false,
        PilotingSkillRollType.BridgeCollapse => false,
        PilotingSkillRollType.CliffFall => false,
        _ => true
    };

    public int GetHeavyDamageThreshold()
    {
        return 20;
    }

    /// <inheritdoc />
    public int GetMaxDisplacementDropWithoutAutomaticFall() => 2;

    private const int AimedShotHeadModifier = 3;
    private const int AimedShotDefaultModifier = -4;

    public int GetAimedShotModifier(PartLocation targetLocation)
    {
        return targetLocation switch
        {
            PartLocation.Head => AimedShotHeadModifier,
            _ => AimedShotDefaultModifier
        };
    }

    // Classic BattleTech aimed shot success on 2D6 rolls of 6, 7, or 8
    private static readonly int[] AimedShotSuccess = [6, 7, 8];

    public int[] GetAimedShotSuccessValues()
    {
        return AimedShotSuccess;
    }

    public int GetHeatShutdownAvoidNumber(int heatLevel)
    {
        return heatLevel switch
        {
            < 14 => 0,  //<14 => always avoid
            <= 17 => 4, //14+ => avoid on 4+
            <= 21 => 6, //18+ => avoid on 6+
            <= 25 => 8, //22+ => avoid on 8+
            <= 29 => 10,//26+ => avoid on 10+
            _ => 13     //30+ => impossible to avoid
        };
    }
    
    private const int ProneFiringModifier = 2;

    public int GetProneFiringModifier()
    {
        return ProneFiringModifier; // +2 modifier for firing while prone
    }

    public int GetHeatAmmoExplosionAvoidNumber(int heatLevel)
    {
        return heatLevel switch
        {
            < 19 => 0,   // <19 => no explosion check
            <= 22 => 4,  // 19+ => avoid on 4+
            <= 27 => 6,  // 23+ => avoid on 6+
            _ => 8       // 28+ => avoid on 8+
        };
    }
    
    public int GetExternalHeatCap()
    {
        return 15;
    }
    
    public int GetWaterHeatDissipationCap()
    {
        return 6;
    }
    
    private const int PartialCoverModifier = 1;

    public int GetPartialCoverModifier()
    {
        return PartialCoverModifier;
    }

    public bool HasPartialCover(IUnit targetUnit, LineOfSightResult losResult)
    {
        // No partial cover if no line of sight or short path
        if (!losResult.HasLineOfSight || losResult.HexPath.Count < 2)
            return false;

        // Get the last hex in the path (target hex)
        var targetHexInfo = losResult.HexPath[^1];
        var targetHex = targetHexInfo.Hex;

        // Validate that the target hex matches the unit's position
        if (targetUnit.Position == null || targetHex.Coordinates != targetUnit.Position.Coordinates)
            return false;

        // Only standing mechs can have partial cover
        if (!targetUnit.CanHavePartialCover)
            return false;

        // Water-based partial cover check (Depth 1 water)
        var waterTerrain = targetHex.GetTerrain(MakaMekTerrains.Water);
        if (waterTerrain is { Height: -1 } && targetUnit.Height > 1)
            return true;

        // Get the hex adjacent to the target (second to last in path)
        var adjacentHexInfo = losResult.HexPath[^2];
        var adjacentHex = adjacentHexInfo.Hex;

        // For partial cover, the adjacent hex must be exactly +1 level higher than target
        var targetLevel = targetHex.Level;
        var adjacentLevel = adjacentHex.Level;
        
        if (adjacentLevel != targetLevel + 1)
            return false;

        // Target height must be >= attacker's height
        if (losResult.TargetLosLevel < losResult.AttackerLosLevel)
            return false;

        return true;
    }

    public bool CanPartBeCovered(PartLocation location)
    {
        return location.IsLeg();
    }

    public int GetWaterDepthModifier(int waterDepth)
    {
        if (waterDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(waterDepth), "Water depth must be at least 1.");
        return waterDepth switch
        {
            1 => -1,  // Depth 1: -1 modifier
            2 => 0,   // Depth 2: 0 modifier
            _ => 1    // Depth 3+: +1 modifier
        };
    }

    /// <summary>
    /// Calculates the total falling damage for a fall that ends in a hex with water, following TW p. 57:
    /// the fall is resolved in two stages — the impact with the water surface (only when the fall
    /// starts above the surface) and the impact with the hex bottom — and the damage of each stage
    /// is halved, rounding down.
    /// </summary>
    /// <param name="perLevelDamage">The damage per fallen level, ceil(tonnage/10)</param>
    /// <param name="levelsFallen">
    /// The total number of levels the unit fell, down to the hex bottom (0 for a fall in place)
    /// </param>
    /// <param name="waterDepth">The water depth of the hex where the fall happens</param>
    /// <returns>The total falling damage after the water reduction</returns>
    public int CalculateFallingDamageInWater(int perLevelDamage, int levelsFallen, int waterDepth)
    {
        if (waterDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(waterDepth), "Water depth must be at least 1.");
        if (levelsFallen < 0)
            throw new ArgumentOutOfRangeException(nameof(levelsFallen), "Levels fallen cannot be negative.");
        if (levelsFallen <= waterDepth)
        {
            // The fall is contained in the water column (from the water surface down to the hex
            // bottom, or a fall in place of a submerged unit): only the impact with the bottom, halved
            return perLevelDamage * (levelsFallen + 1) / 2;
        }

        // The fall starts above the water surface: impact with the surface (halved)
        // plus impact with the hex bottom (halved)
        var surfaceDamage = perLevelDamage * (levelsFallen - waterDepth + 1) / 2;
        var bottomDamage = perLevelDamage * (waterDepth + 1) / 2;
        return surfaceDamage + bottomDamage;
    }

    public int GetSkidModifier(int hexesMoved)
    {
        return hexesMoved switch
        {
            <= 2 => -1,   // 0-2 hexes
            <= 4 => 0,    // 3-4 hexes
            <= 7 => 1,    // 5-7 hexes
            <= 10 => 2,   // 8-10 hexes
            <= 17 => 4,   // 11-17 hexes
            <= 24 => 5,   // 18-24 hexes
            _ => 6        // 25+ hexes
        };
    }

    public int GetProneTargetModifier(int distance)
    {
        if (distance == 1) return -2;
        return 1;
    }
}
