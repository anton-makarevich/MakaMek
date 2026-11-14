using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Resolves the critical-hit rolls of a 'Mech location: how many hits the roll produces, which
/// slots they land on, and whether the location is blown off. Extracted from <see cref="Mech"/>.
/// </summary>
internal static class MechCriticalHitCalculator
{
    /// <summary>
    /// Maximum number of slot-roll attempts before falling back to a uniform pick.
    /// With fair dice, this limit is effectively unreachable.
    /// </summary>
    private const int MaxCriticalSlotRollAttempts = 100;

    /// <summary>
    /// Number of equally likely outcomes of rolling two six-sided dice.
    /// </summary>
    private const int TwoDiceOutcomes = 36;

    /// <summary>
    /// Calculates critical hit data for a specific location and damage.
    /// </summary>
    public static LocationCriticalHitsData? Calculate(
        Mech mech,
        PartLocation location,
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator)
    {
        mech.Parts.TryGetValue(location, out var part);
        if (part is not { CurrentStructure: > 0 })
            return null;

        var critRoll = diceRoller.Roll2D6()
            .Select(d => d.Result)
            .ToArray();
        var numCrits = GetNumCriticalHits(critRoll.Sum());
        ComponentHitData[]? hitComponents = null;

        // Check if the location can be blown off (head or limbs on a roll of 12)
        var isBlownOff = part.CanBeBlownOff && numCrits == 3;
        if (isBlownOff)
        {
            return new LocationCriticalHitsData(location, critRoll, 0, null, isBlownOff);
        }

        if (numCrits > 0)
        {
            hitComponents = DetermineCriticalHitSlots(part, numCrits, diceRoller, damageTransferCalculator);
        }

        return new LocationCriticalHitsData(location, critRoll, numCrits, hitComponents, isBlownOff);
    }

    /// <summary>
    /// Determines the number of critical hits based on the roll.
    /// </summary>
    /// <param name="roll">The 2d6 roll result</param>
    /// <returns>Number of critical hits (0-3)</returns>
    public static int GetNumCriticalHits(int roll)
    {
        // 2–7: 0, 8–9: 1, 10–11: 2, 12: 3 (always return 3 for roll of 12)
        return roll switch
        {
            <= 7 => 0,
            8 or 9 => 1,
            10 or 11 => 2,
            12 => 3,
            _ => 0
        };
    }

    /// <summary>
    /// Determines which specific slots are affected by critical hits.
    /// </summary>
    /// <returns>Array of slot indices affected by critical hits, or null if none</returns>
    private static ComponentHitData[]? DetermineCriticalHitSlots(
        UnitPart part,
        int numCriticalHits,
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator)
    {
        var availableSlots = Enumerable.Range(0, part.TotalSlots)
            .Where(slot => !part.HitSlots.Contains(slot)
                           && part.GetComponentAtSlot(slot) != null)
            .ToList();
        if (availableSlots.Count == 0 || numCriticalHits == 0)
            return null;

        var result = new List<ComponentHitData>();
        for (var i = 0; i < numCriticalHits && availableSlots.Count > 0; i++)
        {
            // With one candidate left, rolling would eventually land on it anyway.
            var slot = availableSlots.Count == 1
                ? availableSlots[0]
                : RollCriticalHitSlot(part, availableSlots, diceRoller);

            result.Add(ComponentHitData.CreateComponentHitData(part, slot, damageTransferCalculator));
            availableSlots.Remove(slot);
        }

        return result.Count > 0 ? result.ToArray() : null;
    }

    /// <summary>
    /// Rolls a critical hit slot per Total Warfare p. 124: 1D6 for a 6-slot location
    /// (head or leg), and two dice for a 12-slot location (torso or arm), where the
    /// first die selects one of the two groups of six slots and the second die selects
    /// the slot within that group. Invalid slots are re-rolled, which gives every
    /// remaining slot an equal chance.
    /// </summary>
    private static int RollCriticalHitSlot(UnitPart part, List<int> availableSlots, IDiceRoller diceRoller)
    {
        var usesTwoDice = part.TotalSlots > 6;

        for (var attempt = 0; attempt < MaxCriticalSlotRollAttempts; attempt++)
        {
            var slot = usesTwoDice
                ? RollTwoDiceSlot(diceRoller)
                : diceRoller.RollD6().Result - 1;

            if (availableSlots.Contains(slot))
                return slot;
        }

        // Degenerate dice must not hang the game, but the fallback still has to keep
        // every remaining slot equally likely instead of always taking the first one.
        return RollSlotUniformly(availableSlots, diceRoller);
    }

    /// <summary>
    /// Picks one of the remaining slots uniformly using two dice. Two dice yield
    /// <see cref="TwoDiceOutcomes"/> equally likely outcomes, so the values that would
    /// make some slots more likely than others are discarded.
    /// </summary>
    private static int RollSlotUniformly(List<int> availableSlots, IDiceRoller diceRoller)
    {
        var limit = TwoDiceOutcomes - TwoDiceOutcomes % availableSlots.Count;
        var roll = TwoDiceOutcomes;

        for (var attempt = 0; attempt < MaxCriticalSlotRollAttempts; attempt++)
        {
            roll = (diceRoller.RollD6().Result - 1) * 6 + diceRoller.RollD6().Result;
            if (roll <= limit)
                break;
        }

        // If the dice never leave the discarded tail, spread the result over the
        // remaining slots rather than always returning the first one.
        return availableSlots[(roll - 1) % availableSlots.Count];
    }

    /// <summary>
    /// Rolls a slot on a 12-slot critical hit table: the first die selects one of the
    /// two groups of six slots, the second die selects the slot within that group.
    /// </summary>
    private static int RollTwoDiceSlot(IDiceRoller diceRoller)
    {
        var groupRoll = diceRoller.RollD6().Result; // 1-3 = slots 0-5, 4-6 = slots 6-11
        var slotRoll = diceRoller.RollD6().Result - 1; // 0-5 within the group
        return (groupRoll <= 3 ? 0 : 6) + slotRoll;
    }
}
