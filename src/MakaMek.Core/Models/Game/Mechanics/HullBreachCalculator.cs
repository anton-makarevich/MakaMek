using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

public class HullBreachCalculator : IHullBreachCalculator
{
    private const int BreachThreshold = 10;
    private readonly IDiceRoller _diceRoller;

    public HullBreachCalculator(IDiceRoller diceRoller)
    {
        _diceRoller = diceRoller;
    }

    public HullBreachCommand? CalculateAndApplyHullBreach(IUnit unit, List<LocationDamageData> damagedLocations)
    {
        if (damagedLocations.Count == 0) return null;

        if (!unit.IsSubmerged) return null;

        var breachedLocations = new List<LocationHullBreachData>();

        foreach (var damagedLocation in damagedLocations)
        {
            if (!unit.Parts.TryGetValue(damagedLocation.Location, out var part)) continue;
            if (part.IsBreached) continue;

            var isAutomatic = part.CurrentArmor <= 0;

            int[]? breachRoll = null;
            if (!isAutomatic)
            {
                var roll = _diceRoller.Roll2D6();
                breachRoll = roll.Select(d => d.Result).ToArray();
                var rollTotal = breachRoll.Sum();

                if (rollTotal < BreachThreshold) continue;
            }

            // Count engine slots at this location
            var engineHitsApplied = 0;
            var engine = unit.GetComponentsAtLocation<Engine>(damagedLocation.Location).FirstOrDefault();
            if (engine != null)
            {
                engineHitsApplied = engine.GetMountedAtLocationSlots(damagedLocation.Location).Length;
            }

            var breachData = new LocationHullBreachData(
                damagedLocation.Location,
                isAutomatic,
                breachRoll,
                null, // Filled after ApplyBreach
                engineHitsApplied
            );

            // Apply breach as side effect (matching CriticalHitsCalculator pattern)
            unit.ApplyHullBreach(breachData);

            // Collect flooded component data after breach so IsFlooded is set
            var floodedComponents = part.Components
                .Select(c => c.ToData())
                .ToArray();

            breachData = breachData with
            {
                FloodedComponents = floodedComponents.Length > 0 ? floodedComponents : null
            };

            breachedLocations.Add(breachData);
        }

        if (breachedLocations.Count == 0) return null;

        return new HullBreachCommand
        {
            GameOriginId = Guid.Empty,
            UnitId = unit.Id,
            BreachedLocations = breachedLocations
        };
    }
}
