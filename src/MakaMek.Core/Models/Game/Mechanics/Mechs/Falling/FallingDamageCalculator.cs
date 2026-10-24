using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

public class FallingDamageCalculator : IFallingDamageCalculator
{
    private readonly ILogger<FallingDamageCalculator> _logger;
    private readonly IDiceRoller _diceRoller;
    private readonly IRulesProvider _rulesProvider;
    private readonly IDamageTransferCalculator _damageTransferCalculator;

    public FallingDamageCalculator(ILogger<FallingDamageCalculator> logger,
        IDiceRoller diceRoller,
        IRulesProvider rulesProvider,
        IDamageTransferCalculator damageTransferCalculator)
    {
        _logger = logger;
        _diceRoller = diceRoller;
        _rulesProvider = rulesProvider;
        _damageTransferCalculator = damageTransferCalculator;
    }

    /// <summary>
    /// Calculates the damage a unit takes when falling
    /// </summary>
    /// <param name="unit">The unit that fell</param>
    /// <param name="levelsFallen">
    /// The total number of levels the unit fell, down to the hex bottom (0 for a fall in place)
    /// </param>
    /// <param name="waterDepth">
    /// Water depth of the hex where the fall happens (0 for dry land).
    /// In depth 1+ water the damage of each impact is halved (BMM p. 57, TW p. 57).
    /// </param>
    /// <returns>The result of the falling damage calculation</returns>
    public FallingDamageData CalculateFallingDamage(
        Unit unit,
        int levelsFallen,
        int waterDepth = 0)
    {
        if (unit is not Mech mech)
        {
            throw new ArgumentException("Only mechs can take falling damage", nameof(unit));
        }

        if (mech.Position == null)
        {
            throw new ArgumentException("Mech must be deployed", nameof(unit));
        }

        var facingRoll = _diceRoller.RollD6();
        var newFacing = _rulesProvider.GetFacingAfterFall(facingRoll.Result, mech.Position.Facing);

        return CalculateFallingDamage(unit, levelsFallen, waterDepth, facingRoll, newFacing);
    }

    /// <summary>
    /// Calculates falling damage with an explicit facing roll.
    /// The total fall (<paramref name="levelsFallen"/>, down to the hex bottom) is split into the impacts
    /// defined by TW: the fall to the water surface (only when the fall starts above it) and the fall
    /// from the surface to the hex bottom. In depth 1+ water the damage of each impact is halved,
    /// rounding down (BMM 7th ed. p. 57, TW p. 57, official clarification 2026-01-22).
    /// </summary>
    /// <param name="unit">The unit that fell</param>
    /// <param name="levelsFallen">
    /// The total number of levels the unit fell, down to the hex bottom (0 for a fall in place)
    /// </param>
    /// <param name="waterDepth">Water depth of the hex where the fall happens (0 for dry land)</param>
    /// <param name="facingDiceRoll">The dice roll for the facing after the fall</param>
    /// <param name="facingAfterFall">The facing after the fall</param>
    /// <returns>The result of the falling damage calculation</returns>
    public FallingDamageData CalculateFallingDamage(
        Unit unit,
        int levelsFallen,
        int waterDepth,
        DiceResult facingDiceRoll,
        HexDirection facingAfterFall)
    {
        if (unit is not Mech mech)
        {
            throw new ArgumentException("Only mechs can take falling damage", nameof(unit));
        }

        if (mech.Position == null)
        {
            throw new ArgumentException("Mech must be deployed", nameof(unit));
        }

        var divisor = _rulesProvider.GetFallingDamageDivisor(waterDepth);
        var perLevelDamage = (int)Math.Ceiling(mech.Tonnage / 10.0);
        int totalDamage;

        if (waterDepth <= 0)
        {
            // Dry land: normal falling damage
            totalDamage = perLevelDamage * (levelsFallen + 1);
        }
        else if (levelsFallen <= waterDepth)
        {
            // The fall is contained in the water column (from the water surface down to the hex bottom,
            // or a fall in place of a submerged unit): only the impact with the bottom, halved
            totalDamage = perLevelDamage * (levelsFallen + 1) / divisor;
        }
        else
        {
            // The fall starts above the water surface: impact with the surface (halved)
            // plus impact with the hex bottom (halved)
            var surfaceDamage = perLevelDamage * (levelsFallen - waterDepth + 1) / divisor;
            var bottomDamage = perLevelDamage * (waterDepth + 1) / divisor;
            totalDamage = surfaceDamage + bottomDamage;
        }

        var attackDirection = _rulesProvider.GetAttackDirectionAfterFall(facingDiceRoll.Result);

        return DistributeDamage(mech, totalDamage, facingAfterFall, facingDiceRoll, attackDirection);
    }

    public FallingDamageData CalculateSkidDamage(Unit unit, int skidDistance, HexDirection facingAfterFall, DiceResult facingRoll, HitDirection attackDirection)
    {
        if (skidDistance < 0)
        {
            _logger.LogError("Skid distance is negative: {SkidDistance}", skidDistance);
            throw new ArgumentOutOfRangeException(nameof(skidDistance), "Skid distance must be non-negative");
        }

        if (unit.Position == null)
        {
            throw new ArgumentException("Unit must be deployed", nameof(unit));
        }

        var damagePerHex = Math.Ceiling(unit.Tonnage / 10.0) * 0.5;
        var totalDamage = (int)Math.Ceiling(damagePerHex * skidDistance);

        return DistributeDamage(unit, totalDamage, facingAfterFall, facingRoll, attackDirection);
    }

    private FallingDamageData DistributeDamage(Unit unit, int totalDamage, HexDirection newFacing, DiceResult facingRoll, HitDirection attackDirection)
    {
        var hitLocations = new List<LocationHitData>();

        var fullGroups = totalDamage / 5;
        var remainingPoints = totalDamage % 5;

        for (var i = 0; i < fullGroups; i++)
        {
            hitLocations.Add(DetermineHitLocationData(5));
        }

        if (remainingPoints > 0)
        {
            hitLocations.Add(DetermineHitLocationData(remainingPoints));
        }

        var hitLocationsData = new HitLocationsData(
            hitLocations,
            totalDamage
        );

        return new FallingDamageData(
            newFacing,
            hitLocationsData,
            facingRoll,
            attackDirection
        );

        LocationHitData DetermineHitLocationData(int damageAmount)
        {
            var locationRolls = _diceRoller.Roll2D6().Select(d => d.Result).ToArray();
            var locationRollResult = locationRolls.Sum();
            var hitLocation = _rulesProvider.GetHitLocation(locationRollResult, attackDirection);

            var locationDamage = _damageTransferCalculator.CalculateStructureDamage(
                unit,
                hitLocation,
                damageAmount,
                attackDirection);

            return new LocationHitData(
                locationDamage,
                [],
                locationRolls,
                hitLocation
            );
        }
    }
}
