using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Utils.TechRules;

public class ClassicBattletechRulesProvider : IRulesProvider
{
    public Dictionary<PartLocation, int> GetStructureValues(int tonnage)
    {
        var structureValues = new Dictionary<PartLocation, int>();

        // Structure values based on tonnage
        switch (tonnage)
        {
            case 20:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 6;
                structureValues[PartLocation.LeftTorso] = 5;
                structureValues[PartLocation.RightTorso] = 5;
                structureValues[PartLocation.LeftArm] = 3;
                structureValues[PartLocation.RightArm] = 3;
                structureValues[PartLocation.LeftLeg] = 4;
                structureValues[PartLocation.RightLeg] = 4;
                break;
            case 25:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 8;
                structureValues[PartLocation.LeftTorso] = 6;
                structureValues[PartLocation.RightTorso] = 6;
                structureValues[PartLocation.LeftArm] = 4;
                structureValues[PartLocation.RightArm] = 4;
                structureValues[PartLocation.LeftLeg] = 6;
                structureValues[PartLocation.RightLeg] = 6;
                break;
            case 30:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 10;
                structureValues[PartLocation.LeftTorso] = 7;
                structureValues[PartLocation.RightTorso] = 7;
                structureValues[PartLocation.LeftArm] = 5;
                structureValues[PartLocation.RightArm] = 5;
                structureValues[PartLocation.LeftLeg] = 7;
                structureValues[PartLocation.RightLeg] = 7;
                break;
            case 35:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 11;
                structureValues[PartLocation.LeftTorso] = 8;
                structureValues[PartLocation.RightTorso] = 8;
                structureValues[PartLocation.LeftArm] = 6;
                structureValues[PartLocation.RightArm] = 6;
                structureValues[PartLocation.LeftLeg] = 8;
                structureValues[PartLocation.RightLeg] = 8;
                break;
            case 40:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 12;
                structureValues[PartLocation.LeftTorso] = 10;
                structureValues[PartLocation.RightTorso] = 10;
                structureValues[PartLocation.LeftArm] = 6;
                structureValues[PartLocation.RightArm] = 6;
                structureValues[PartLocation.LeftLeg] = 10;
                structureValues[PartLocation.RightLeg] = 10;
                break;
            case 45:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 14;
                structureValues[PartLocation.LeftTorso] = 11;
                structureValues[PartLocation.RightTorso] = 11;
                structureValues[PartLocation.LeftArm] = 7;
                structureValues[PartLocation.RightArm] = 7;
                structureValues[PartLocation.LeftLeg] = 11;
                structureValues[PartLocation.RightLeg] = 11;
                break;
            case 50:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 16;
                structureValues[PartLocation.LeftTorso] = 12;
                structureValues[PartLocation.RightTorso] = 12;
                structureValues[PartLocation.LeftArm] = 8;
                structureValues[PartLocation.RightArm] = 8;
                structureValues[PartLocation.LeftLeg] = 12;
                structureValues[PartLocation.RightLeg] = 12;
                break;
            case 55:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 18;
                structureValues[PartLocation.LeftTorso] = 13;
                structureValues[PartLocation.RightTorso] = 13;
                structureValues[PartLocation.LeftArm] = 9;
                structureValues[PartLocation.RightArm] = 9;
                structureValues[PartLocation.LeftLeg] = 13;
                structureValues[PartLocation.RightLeg] = 13;
                break;
            case 60:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 20;
                structureValues[PartLocation.LeftTorso] = 14;
                structureValues[PartLocation.RightTorso] = 14;
                structureValues[PartLocation.LeftArm] = 10;
                structureValues[PartLocation.RightArm] = 10;
                structureValues[PartLocation.LeftLeg] = 14;
                structureValues[PartLocation.RightLeg] = 14;
                break;
            case 65:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 21;
                structureValues[PartLocation.LeftTorso] = 15;
                structureValues[PartLocation.RightTorso] = 15;
                structureValues[PartLocation.LeftArm] = 10;
                structureValues[PartLocation.RightArm] = 10;
                structureValues[PartLocation.LeftLeg] = 15;
                structureValues[PartLocation.RightLeg] = 15;
                break;
            case 70:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 22;
                structureValues[PartLocation.LeftTorso] = 15;
                structureValues[PartLocation.RightTorso] = 15;
                structureValues[PartLocation.LeftArm] = 11;
                structureValues[PartLocation.RightArm] = 11;
                structureValues[PartLocation.LeftLeg] = 15;
                structureValues[PartLocation.RightLeg] = 15;
                break;
            case 75:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 23;
                structureValues[PartLocation.LeftTorso] = 16;
                structureValues[PartLocation.RightTorso] = 16;
                structureValues[PartLocation.LeftArm] = 12;
                structureValues[PartLocation.RightArm] = 12;
                structureValues[PartLocation.LeftLeg] = 16;
                structureValues[PartLocation.RightLeg] = 16;
                break;
            case 80:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 25;
                structureValues[PartLocation.LeftTorso] = 17;
                structureValues[PartLocation.RightTorso] = 17;
                structureValues[PartLocation.LeftArm] = 13;
                structureValues[PartLocation.RightArm] = 13;
                structureValues[PartLocation.LeftLeg] = 17;
                structureValues[PartLocation.RightLeg] = 17;
                break;
            case 85:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 27;
                structureValues[PartLocation.LeftTorso] = 18;
                structureValues[PartLocation.RightTorso] = 18;
                structureValues[PartLocation.LeftArm] = 14;
                structureValues[PartLocation.RightArm] = 14;
                structureValues[PartLocation.LeftLeg] = 18;
                structureValues[PartLocation.RightLeg] = 18;
                break;
            case 90:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 29;
                structureValues[PartLocation.LeftTorso] = 19;
                structureValues[PartLocation.RightTorso] = 19;
                structureValues[PartLocation.LeftArm] = 15;
                structureValues[PartLocation.RightArm] = 15;
                structureValues[PartLocation.LeftLeg] = 19;
                structureValues[PartLocation.RightLeg] = 19;
                break;
            case 95:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 30;
                structureValues[PartLocation.LeftTorso] = 20;
                structureValues[PartLocation.RightTorso] = 20;
                structureValues[PartLocation.LeftArm] = 16;
                structureValues[PartLocation.RightArm] = 16;
                structureValues[PartLocation.LeftLeg] = 20;
                structureValues[PartLocation.RightLeg] = 20;
                break;
            case 100:
                structureValues[PartLocation.Head] = 3;
                structureValues[PartLocation.CenterTorso] = 31;
                structureValues[PartLocation.LeftTorso] = 21;
                structureValues[PartLocation.RightTorso] = 21;
                structureValues[PartLocation.LeftArm] = 17;
                structureValues[PartLocation.RightArm] = 17;
                structureValues[PartLocation.LeftLeg] = 21;
                structureValues[PartLocation.RightLeg] = 21;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(tonnage), "Invalid tonnage");
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
            <= 2 => 0,    // 0-2 hexes: no modifier
            <= 4 => 1,    // 3-4 hexes: +1
            <= 6 => 2,    // 5-6 hexes: +2
            <= 9 => 3,    // 7-9 hexes: +3
            <= 17 => 4,   // 10-17 hexes: +4
            <= 24 => 5,   // 18-24 hexes: +5
            _ => 6        // 25+ hexes: +6
        };
    }

    public int GetRangeModifier(WeaponRange rangeType, int rangeValue, int distance)
    {
        return rangeType switch
        {
            WeaponRange.Minimum => rangeValue-distance+1,
            WeaponRange.Short => 0,
            WeaponRange.Medium => 2,
            WeaponRange.Long => 4,
            WeaponRange.OutOfRange => ToHitBreakdown.ImpossibleRoll,
            _ => throw new ArgumentException($"Unknown weapon range: {rangeType}")
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

    public PartLocation GetHitLocation(int diceResult, FiringArc attackDirection)
    {
        // Classic BattleTech hit location tables based on the attack direction
        // Note: Front and Rear directions use the same hit location table
        return attackDirection switch
        {
            FiringArc.Forward or FiringArc.Rear => diceResult switch
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
            FiringArc.Left => diceResult switch
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
            FiringArc.Right => diceResult switch
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
        var clusterHitsTable = new[,] {
           // 2,  4,  5,  6,  10, 15, 20 (weapon sizes)
            { 1,  1,  1,  2,  3,  5,  6  },  // Roll of 2
            { 1,  2,  2,  2,  3,  5,  6  },  // Roll of 3
            { 1,  2,  2,  3,  4,  6,  9  },  // Roll of 4
            { 1,  2,  3,  3,  6,  9,  12 },  // Roll of 5
            { 1,  2,  3,  4,  6,  9,  12 },  // Roll of 6
            { 1,  3,  3,  4,  6,  9,  12 },  // Roll of 7
            { 2,  3,  3,  4,  6,  9,  12 },  // Roll of 8
            { 2,  3,  4,  5,  8,  12, 16 },  // Roll of 9
            { 2,  3,  4,  5,  8,  12, 16 },  // Roll of 10
            { 2,  4,  5,  6,  10, 15, 20 },  // Roll of 11
            { 2,  4,  5,  6,  10, 15, 20 }   // Roll of 12
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
}