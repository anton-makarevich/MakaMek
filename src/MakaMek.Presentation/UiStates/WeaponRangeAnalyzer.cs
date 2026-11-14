using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Works out where an attacker's weapons can fire: the firing arc of each mount, the line of sight
/// per mount level, the resulting per-weapon reachable hexes, and the highlights that visualise them.
/// </summary>
/// <remarks>
/// Kept outside <see cref="WeaponsAttackState"/> so the state only stores the result and applies one
/// batched highlight update, and so the geometry can be exercised without a UI state.
/// </remarks>
public sealed class WeaponRangeAnalyzer
{
    /// <summary>
    /// Computes the reachable hexes of the supplied weapon candidates and, through
    /// <paramref name="losHighlights"/>, the hexes whose line of sight is blocked from every mount
    /// level. A missing map skips the line-of-sight checks, exactly like the uncached pre-move state.
    /// </summary>
    public Dictionary<Weapon, HashSet<HexCoordinates>> Analyze(
        IUnit attacker,
        IReadOnlyList<WeaponCandidate> weaponCandidates,
        IEnumerable<IUnit> otherUnits,
        IBattleMap? map,
        out Dictionary<HexCoordinates, LosBlockingHighlight> losHighlights)
    {
        var losResults = ComputeLineOfSightResults(attacker, otherUnits, map, weaponCandidates);
        var ranges = PopulateWeaponRanges(weaponCandidates, losResults, map);

        losHighlights = BuildLineOfSightBlockingHighlights(weaponCandidates, losResults, map);
        return ranges;
    }

    /// <summary>
    /// Deduplicates LOS checks by unique mount level. A hex can hold more than one unit, and a
    /// pending DFA attacker is airborne while a co-located unit stays grounded, so a hex result must
    /// consider every occupant instead of the first one found.
    /// </summary>
    private static Dictionary<(HexCoordinates Hex, int Level), LineOfSightResult> ComputeLineOfSightResults(
        IUnit attacker,
        IEnumerable<IUnit> otherUnits,
        IBattleMap? map,
        IReadOnlyList<WeaponCandidate> weaponCandidates)
    {
        var losResults = new Dictionary<(HexCoordinates Hex, int Level), LineOfSightResult>();
        if (map == null) return losResults;

        var occupantsByHex = otherUnits
            .Where(unit => unit.Position != null && !ReferenceEquals(unit, attacker))
            .GroupBy(unit => unit.Position!.Coordinates)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var (mountLevel, hexes) in UnionHexesByMountLevel(weaponCandidates))
        {
            foreach (var hex in hexes)
            {
                losResults[(hex, mountLevel)] = GetLineOfSightToHex(
                    map,
                    attacker,
                    mountLevel,
                    hex,
                    occupantsByHex.GetValueOrDefault(hex));
            }
        }

        return losResults;
    }

    private static Dictionary<int, HashSet<HexCoordinates>> UnionHexesByMountLevel(
        IReadOnlyList<WeaponCandidate> weaponCandidates) =>
        weaponCandidates
            .GroupBy(candidate => candidate.MountLevel)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(candidate => candidate.CandidateHexes).ToHashSet());

    private static LineOfSightResult GetLineOfSightToHex(
        IBattleMap map,
        IUnit attacker,
        int mountLevel,
        HexCoordinates hex,
        IReadOnlyList<IUnit>? occupants)
    {
        if (occupants is { Count: > 0 })
        {
            LineOfSightResult? firstResult = null;
            foreach (var occupant in occupants)
            {
                var occupantResult = GetLineOfSightToUnit(map, attacker, mountLevel, occupant);
                if (occupantResult.HasLineOfSight) return occupantResult;
                firstResult ??= occupantResult;
            }

            return firstResult!;
        }

        var attackerPosition = attacker.Position!;
        return map.GetLineOfSight(
            attackerPosition.Coordinates,
            hex,
            mountLevel,
            0,
            attackerPosition.Surface,
            null,
            attackerPosition.AirborneLevel,
            null);
    }

    private static LineOfSightResult GetLineOfSightToUnit(
        IBattleMap map,
        IUnit attacker,
        int mountLevel,
        IUnit target)
    {
        var attackerPosition = attacker.Position!;
        var targetPosition = target.Position!;
        return map.GetLineOfSight(attackerPosition, targetPosition, mountLevel, target.Height);
    }

    /// <summary>
    /// Keeps only the candidate hexes whose line of sight is not blocked from the weapon's mount level.
    /// </summary>
    private static Dictionary<Weapon, HashSet<HexCoordinates>> PopulateWeaponRanges(
        IReadOnlyList<WeaponCandidate> weaponCandidates,
        Dictionary<(HexCoordinates Hex, int Level), LineOfSightResult> losResults,
        IBattleMap? map)
    {
        var ranges = new Dictionary<Weapon, HashSet<HexCoordinates>>(weaponCandidates.Count);

        foreach (var candidate in weaponCandidates)
        {
            var filteredHexes = new HashSet<HexCoordinates>(candidate.CandidateHexes);

            if (map != null)
            {
                filteredHexes.RemoveWhere(hex => IsLineOfSightBlocked(hex, candidate.MountLevel, losResults));
            }

            ranges[candidate.Weapon] = filteredHexes;
        }

        return ranges;
    }

    private static bool IsLineOfSightBlocked(
        HexCoordinates hex,
        int mountLevel,
        Dictionary<(HexCoordinates Hex, int Level), LineOfSightResult> losResults) =>
        losResults.TryGetValue((hex, mountLevel), out var result) && !result.HasLineOfSight;

    /// <summary>
    /// A hex is marked as blocked only when every mount level that could reach it fails LOS.
    /// </summary>
    private static Dictionary<HexCoordinates, LosBlockingHighlight> BuildLineOfSightBlockingHighlights(
        IReadOnlyList<WeaponCandidate> weaponCandidates,
        Dictionary<(HexCoordinates Hex, int Level), LineOfSightResult> losResults,
        IBattleMap? map)
    {
        var losHighlights = new Dictionary<HexCoordinates, LosBlockingHighlight>();
        if (map == null) return losHighlights;

        foreach (var (hex, levels) in CollectLevelsPerHex(weaponCandidates))
        {
            var highlight = CreateBlockingHighlight(hex, levels, losResults);
            if (highlight is not null) losHighlights[hex] = highlight;
        }

        return losHighlights;
    }

    private static Dictionary<HexCoordinates, List<int>> CollectLevelsPerHex(
        IReadOnlyList<WeaponCandidate> weaponCandidates)
    {
        // Pre-compute hex → distinct levels for O(1) lookup per hex
        var hexToLevels = new Dictionary<HexCoordinates, List<int>>();

        foreach (var candidate in weaponCandidates)
        {
            foreach (var hex in candidate.CandidateHexes)
            {
                if (!hexToLevels.TryGetValue(hex, out var levels))
                {
                    levels = [];
                    hexToLevels[hex] = levels;
                }

                if (!levels.Contains(candidate.MountLevel))
                    levels.Add(candidate.MountLevel);
            }
        }

        return hexToLevels;
    }

    private static LosBlockingHighlight? CreateBlockingHighlight(
        HexCoordinates hex,
        List<int> levels,
        Dictionary<(HexCoordinates Hex, int Level), LineOfSightResult> losResults)
    {
        var allLevelsBlocked = levels.Count > 0 && levels.All(level =>
            losResults.TryGetValue((hex, level), out var result)
            && !result.HasLineOfSight);
        if (!allLevelsBlocked) return null;

        var firstLevelResult = losResults[(hex, levels[0])];
        if (firstLevelResult is not { HasLineOfSight: false, BlockReason: { } reason }) return null;

        return new LosBlockingHighlight(reason, firstLevelResult.BlockingHexCoordinates);
    }

    /// <summary>
    /// One reachable highlight per hex, labelled with the names of the weapons that can fire into it.
    /// </summary>
    private static Dictionary<HexCoordinates, AttackReachableHighlight> BuildReachableHighlights(
        IReadOnlyDictionary<Weapon, HashSet<HexCoordinates>> weaponRanges)
    {
        var hexToWeaponNames = new Dictionary<HexCoordinates, HashSet<string>>();
        foreach (var (weapon, hexes) in weaponRanges)
        {
            foreach (var hex in hexes)
            {
                if (!hexToWeaponNames.TryGetValue(hex, out var names))
                {
                    names = [];
                    hexToWeaponNames[hex] = names;
                }

                names.Add(weapon.Name);
            }
        }

        return hexToWeaponNames.ToDictionary(
            pair => pair.Key,
            pair => new AttackReachableHighlight(pair.Value.OrderBy(name => name).ToList()));
    }

    /// <summary>One weapon that can fire, with the hexes it can reach and the level LOS is checked from.</summary>
    public readonly record struct WeaponCandidate(
        Weapon Weapon,
        HashSet<HexCoordinates> CandidateHexes,
        int MountLevel);
}
