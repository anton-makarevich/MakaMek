using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Classic BattleTech implementation of falling damage calculator
/// </summary>
public class FallingDamageCalculator : IFallingDamageCalculator
{
    private readonly IDiceRoller _diceRoller;
    private readonly IRulesProvider _rulesProvider;

    public FallingDamageCalculator(IDiceRoller diceRoller, IRulesProvider rulesProvider)
    {
        _diceRoller = diceRoller;
        _rulesProvider = rulesProvider;
    }

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

        if (mech.Position == null)
        {
            throw new ArgumentException("Mech must be deployed", nameof(unit)); 
        }

        // Calculate damage per group based on tonnage (rounded up to nearest 10)
        var damagePerGroup = (int)Math.Ceiling(mech.Tonnage / 10.0);
        
        // If the mech was jumping, it only takes damage for 1 level
        // Otherwise, it takes damage for (levelsFallen + 1) levels
        var effectiveLevels = wasJumping ? 0 : levelsFallen;
        var totalDamage = damagePerGroup * (effectiveLevels + 1);
        
        // Roll for facing after fall (1d6)
        var facingRoll = _diceRoller.RollD6();
        
        // Determine new facing based on current facing and roll
        HexDirection newFacing = _rulesProvider.GetFacingAfterFall(facingRoll.Result, mech.Position.Facing);
        
        // Determine attack direction for hit location purposes
        FiringArc attackDirection = _rulesProvider.GetAttackDirectionAfterFall(facingRoll.Result);
        
        // Roll for hit location (2d6)
        var locationRolls = _diceRoller.Roll2D6();
        var locationRollResult = locationRolls.Sum(r => r.Result);
        
        // Get hit location from rules provider
        var hitLocation = _rulesProvider.GetHitLocation(locationRollResult, attackDirection);
        
        // Create a list of hit locations
        var hitLocations = new List<HitLocationData>
        {
            new HitLocationData(
                hitLocation,
                totalDamage,
                locationRolls
            )
        };
        
        // Create hit locations data
        var hitLocationsData = new HitLocationsData(
            hitLocations,
            totalDamage
        );
        
        return new FallingDamageData(
            totalDamage,
            damagePerGroup,
            newFacing,
            hitLocationsData,
            facingRoll
        );
    }
    
    /// <summary>
    /// Determines if a pilot takes damage from a fall
    /// </summary>
    /// <param name="unit">The unit that fell</param>
    /// <param name="levelsFallen">The number of levels the unit fell</param>
    /// <param name="psrBreakdown">The piloting skill roll breakdown</param>
    /// <param name="diceRolls">The dice roll result</param>
    /// <returns>True if the pilot takes damage, false otherwise</returns>
    public bool DeterminePilotDamage(Unit unit, int levelsFallen, PsrBreakdown psrBreakdown, List<DiceResult> diceRolls)
    {
        if (unit is not Mech mech)
        {
            throw new ArgumentException("Only mech pilots can take falling damage", nameof(unit));
        }
        
        // Pilot automatically takes damage if:
        // - Mech is immobile
        // - Pilot is unconscious
        // - PSR target number is impossible (> 12)
        if ((mech.Status & UnitStatus.Immobile) != 0 ||
            mech.Crew?.IsUnconscious == true ||
            psrBreakdown.IsImpossible)
        {
            return true;
        }
        
        // Otherwise, pilot takes damage if PSR fails
        int rollResult = diceRolls.Sum(r => r.Result);
        return rollResult > psrBreakdown.ModifiedPilotingSkill;
    }
}
