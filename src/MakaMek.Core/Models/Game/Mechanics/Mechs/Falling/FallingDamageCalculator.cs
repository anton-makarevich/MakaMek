using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

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
    public FallingDamageData CalculateFallingDamage(
        Unit unit, 
        int levelsFallen, 
        bool wasJumping)
    {
        if (unit is not Mech mech)
        {
            throw new ArgumentException("Only mechs can take falling damage", nameof(unit));
        }

        if (mech.Position == null)
        {
            throw new ArgumentException("Mech must be deployed", nameof(unit)); 
        }

        // If the mech was jumping, it only takes damage for 1 level
        // Otherwise, it takes damage for (levelsFallen + 1) levels
        var effectiveLevels = wasJumping ? 0 : levelsFallen;
        
        // Calculate damage based on tonnage (rounded up to nearest 10)
        var totalDamage = (int)Math.Ceiling(mech.Tonnage / 10.0)*(effectiveLevels + 1);
        
        // Roll for facing after fall (1d6)
        var facingRoll = _diceRoller.RollD6();
        
        // Determine new facing based on current facing and roll
        var newFacing = _rulesProvider.GetFacingAfterFall(facingRoll.Result, mech.Position.Facing);
        
        // Determine attack direction for hit location purposes
        var attackDirection = _rulesProvider.GetAttackDirectionAfterFall(facingRoll.Result);
        
        // Divide damage into groups of 5 points each
        var hitLocations = new List<HitLocationData>();
        var remainingDamage = totalDamage;
        
        // Calculate how many full 5-point groups we have
        var fullGroups = remainingDamage / 5;
        // Calculate the remaining points for the smaller group
        var remainingPoints = remainingDamage % 5;
        
        // Local function to determine hit location and create HitLocationData
        HitLocationData DetermineHitLocationData(int damageAmount)
        {
            var locationRolls = _diceRoller.Roll2D6().Select(d => d.Result).ToArray();
            var locationRollResult = locationRolls.Sum();
            var hitLocation = _rulesProvider.GetHitLocation(locationRollResult, attackDirection);
            
            return new HitLocationData(
                hitLocation,
                damageAmount,
                [],
                locationRolls
            );
        }
        
        // Add full 5-point groups
        for (var i = 0; i < fullGroups; i++)
        {
            hitLocations.Add(DetermineHitLocationData(5));
        }
        
        // Add the smaller group if there are remaining points
        if (remainingPoints > 0)
        {
            hitLocations.Add(DetermineHitLocationData(remainingPoints));
        }
        
        // Create hit locations data
        var hitLocationsData = new HitLocationsData(
            hitLocations,
            totalDamage
        );

        return new FallingDamageData(
            newFacing,
            hitLocationsData,
            facingRoll
        );
    }
}
