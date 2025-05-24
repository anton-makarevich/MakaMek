using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Classic BattleTech implementation of falling damage calculator
/// </summary>
public class FallingDamageCalculator : IFallingDamageCalculator
{
    private readonly Random _random = new();

    /// <summary>
    /// Calculates the damage a unit takes when falling
    /// </summary>
    /// <param name="unit">The unit that fell</param>
    /// <param name="levelsFallen">The number of levels the unit fell</param>
    /// <param name="wasJumping">Whether the unit was jumping when it fell</param>
    /// <returns>The result of the falling damage calculation</returns>
    public FallingDamageData CalculateFallingDamage(Unit unit, int levelsFallen, bool wasJumping)
    {
        if (unit is not Mech mech)
        {
            throw new ArgumentException("Only mechs can take falling damage", nameof(unit));
        }

        // Calculate damage per group based on tonnage (rounded up to nearest 10)
        int damagePerGroup = (int)Math.Ceiling(mech.Tonnage / 10.0);
        
        // If the mech was jumping, it only takes damage for 1 level
        // Otherwise, it takes damage for (levelsFallen + 1) levels
        int effectiveLevels = wasJumping ? 0 : levelsFallen;
        int totalDamage = damagePerGroup * (effectiveLevels + 1);
        
        // Roll for facing after fall (1d6)
        var facingRoll = new DiceRoll(1, 6);
        facingRoll.Roll();
        
        // Convert roll to facing
        HexDirection newFacing = RollToFacing(facingRoll.Result);
        
        // Determine hit location (2d6)
        var locationRoll = new DiceRoll(2, 6);
        locationRoll.Roll();
        
        MechHitLocation hitLocation = DetermineHitLocation(locationRoll.Result);
        
        // Create a HitLocationData object
        var hitLocationData = new HitLocationData(
            ConvertMechHitLocationToPartLocation(hitLocation),
            totalDamage,
            new List<DiceResult> { new DiceResult(locationRoll.Result, locationRoll.DiceCount, locationRoll.DiceSides) }
        );
        
        return new FallingDamageData(
            totalDamage,
            damagePerGroup,
            newFacing,
            hitLocation,
            facingRoll,
            false, // WarriorTakesDamage will be determined separately
            null,   // WarriorDamageRoll will be set if needed
            hitLocationData
        );
    }
    
    /// <summary>
    /// Determines if a warrior takes damage from a fall
    /// </summary>
    /// <param name="unit">The unit that fell</param>
    /// <param name="levelsFallen">The number of levels the unit fell</param>
    /// <param name="psrBreakdown">The piloting skill roll breakdown</param>
    /// <param name="diceRoll">The dice roll result</param>
    /// <returns>True if the warrior takes damage, false otherwise</returns>
    public bool DetermineWarriorDamage(Unit unit, int levelsFallen, PsrBreakdown psrBreakdown, DiceResult diceRoll)
    {
        if (unit is not Mech mech)
        {
            throw new ArgumentException("Only mech warriors can take falling damage", nameof(unit));
        }
        
        // Warrior automatically takes damage if:
        // - Mech is immobile
        // - Warrior is unconscious
        // - PSR target number is impossible (> 12)
        if ((mech.Status & UnitStatus.Immobile) != 0 ||
            mech.Crew?.IsUnconscious == true ||
            psrBreakdown.TargetNumber > 12)
        {
            return true;
        }
        
        // Otherwise, warrior takes damage if PSR fails
        return diceRoll.Result > psrBreakdown.TargetNumber;
    }
    
    /// <summary>
    /// Converts a dice roll to a facing direction
    /// </summary>
    /// <param name="roll">The dice roll result (1-6)</param>
    /// <returns>The new facing direction</returns>
    private HexDirection RollToFacing(int roll)
    {
        return roll switch
        {
            1 => HexDirection.North,
            2 => HexDirection.NorthEast,
            3 => HexDirection.SouthEast,
            4 => HexDirection.South,
            5 => HexDirection.SouthWest,
            6 => HexDirection.NorthWest,
            _ => throw new ArgumentOutOfRangeException(nameof(roll), "Roll must be between 1 and 6")
        };
    }
    
    /// <summary>
    /// Determines the hit location based on a 2d6 roll
    /// </summary>
    /// <param name="roll">The dice roll result (2-12)</param>
    /// <returns>The hit location</returns>
    private MechHitLocation DetermineHitLocation(int roll)
    {
        return roll switch
        {
            2 => MechHitLocation.CenterTorso,
            3 => MechHitLocation.RightArm,
            4 => MechHitLocation.RightArm,
            5 => MechHitLocation.RightLeg,
            6 => MechHitLocation.RightLeg,
            7 => MechHitLocation.CenterTorso,
            8 => MechHitLocation.LeftLeg,
            9 => MechHitLocation.LeftLeg,
            10 => MechHitLocation.LeftArm,
            11 => MechHitLocation.LeftArm,
            12 => MechHitLocation.Head,
            _ => throw new ArgumentOutOfRangeException(nameof(roll), "Roll must be between 2 and 12")
        };
    }
    
    /// <summary>
    /// Converts a MechHitLocation to a PartLocation
    /// </summary>
    /// <param name="location">The MechHitLocation to convert</param>
    /// <returns>The corresponding PartLocation</returns>
    private PartLocation ConvertMechHitLocationToPartLocation(MechHitLocation location)
    {
        return location switch
        {
            MechHitLocation.Head => PartLocation.Head,
            MechHitLocation.CenterTorso => PartLocation.CenterTorso,
            MechHitLocation.LeftTorso => PartLocation.LeftTorso,
            MechHitLocation.RightTorso => PartLocation.RightTorso,
            MechHitLocation.LeftArm => PartLocation.LeftArm,
            MechHitLocation.RightArm => PartLocation.RightArm,
            MechHitLocation.LeftLeg => PartLocation.LeftLeg,
            MechHitLocation.RightLeg => PartLocation.RightLeg,
            _ => throw new ArgumentOutOfRangeException(nameof(location), "Unknown MechHitLocation")
        };
    }
}
