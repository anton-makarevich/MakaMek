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
            var breachData = BreachLocation(unit, damagedLocation);
            if (breachData != null)
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

    /// <summary>
    /// Breaches one damaged location, applying the breach as a side effect (matching the
    /// <see cref="CriticalHitsCalculator"/> pattern) and returning the data to broadcast.
    /// </summary>
    private LocationHullBreachData? BreachLocation(IUnit unit, LocationDamageData damagedLocation)
    {
        if (!unit.Parts.TryGetValue(damagedLocation.Location, out var part)) return null;
        if (part.IsBreached) return null;

        var isAutomatic = part.CurrentArmor <= 0;
        var breachRoll = isAutomatic ? null : RollBreach();
        if (breachRoll is { } roll && roll.Sum() < BreachThreshold) return null;

        var breachData = new LocationHullBreachData(
            damagedLocation.Location,
            isAutomatic,
            breachRoll,
            null, // Filled after ApplyBreach
            CountEngineHits(unit, damagedLocation.Location));

        unit.ApplyHullBreach(breachData);

        // Collect flooded component data after breach so IsFlooded is set
        var floodedComponents = part.Components
            .Select(c => c.ToData())
            .ToArray();

        return breachData with
        {
            FloodedComponents = floodedComponents.Length > 0 ? floodedComponents : null
        };
    }

    private int[] RollBreach() =>
        _diceRoller.Roll2D6().Select(d => d.Result).ToArray();

    private static int CountEngineHits(IUnit unit, PartLocation location)
    {
        var engine = unit.GetComponentsAtLocation<Engine>(location).FirstOrDefault();
        return engine?.GetMountedAtLocationSlots(location).Length ?? 0;
    }
}
