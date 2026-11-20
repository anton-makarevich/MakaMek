using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Works out and highlights where an attacker's weapons can fire, and answers range questions for the
/// current attacker. Kept out of <see cref="WeaponsAttackState"/> so the state only stores the result
/// and orchestrates the selection steps.
/// </summary>
public sealed class WeaponRangeHighlighter
{
    private readonly BattleMapViewModel _viewModel;
    private IReadOnlyDictionary<Weapon, HashSet<HexCoordinates>> _weaponRanges =
        new Dictionary<Weapon, HashSet<HexCoordinates>>();

    internal WeaponRangeHighlighter(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    /// <summary>
    /// The per-weapon reachable hexes for the current attacker.
    /// </summary>
    public IReadOnlyDictionary<Weapon, HashSet<HexCoordinates>> WeaponRanges => _weaponRanges;

    public void Clear()
    {
        _viewModel.ClearHighlights();
        _weaponRanges = new Dictionary<Weapon, HashSet<HexCoordinates>>();
    }

    public void Highlight(IUnit attacker, IClientGame game)
    {
        if (attacker.Position is not { } attackerPosition) return;

        var candidates = CollectWeaponCandidates(attacker, game, attackerPosition);
        var analysis = attacker.AnalyzeWeaponRanges(
            candidates,
            _viewModel.Units,
            game.BattleMap);
        _weaponRanges = analysis.Ranges;

        // Apply all highlights in a single batch call so boundary outlines
        // are computed once over the full region per highlight type.
        var perHexHighlights = new Dictionary<HexCoordinates, IHexHighlightType>(
            analysis.LosHighlights.Count + analysis.ReachableHighlights.Count);
        foreach (var (coordinates, highlight) in analysis.LosHighlights) perHexHighlights[coordinates] = highlight;
        foreach (var (coordinates, highlight) in analysis.ReachableHighlights) perHexHighlights[coordinates] = highlight;
        _viewModel.HighlightRegions(perHexHighlights);
    }

    /// <summary>
    /// Collects the firing-arc hexes of every weapon that can fire, together with the mount level
    /// line of sight must be checked from. No line-of-sight checks happen here.
    /// </summary>
    private static List<WeaponRangeAnalyzerExtensions.WeaponCandidate> CollectWeaponCandidates(
        IUnit attacker,
        IClientGame game,
        HexPosition unitPosition)
    {
        var candidates = new List<WeaponRangeAnalyzerExtensions.WeaponCandidate>();

        foreach (var part in attacker.Parts.Values)
        {
            foreach (var weapon in part.GetComponents<Weapon>())
            {
                var candidate = CreateWeaponCandidate(weapon, game, unitPosition.Coordinates);
                if (candidate is not null) candidates.Add(candidate.Value);
            }
        }

        return candidates;
    }

    private static WeaponRangeAnalyzerExtensions.WeaponCandidate? CreateWeaponCandidate(
        Weapon weapon,
        IClientGame game,
        HexCoordinates origin)
    {
        // Skip weapons that cannot fire at all
        if (!weapon.IsAvailableForAttack()
            || weapon.Range == null
            || weapon.FirstMountPart == null)
        {
            return null;
        }

        var facing = weapon.FirstMountPart.Facing;
        if (facing == null) return null;

        var weaponHexes = new HashSet<HexCoordinates>();
        // For arms, we need to check both forward and side arcs
        foreach (var arc in weapon.GetFiringArcs())
        {
            var hexes = origin.GetHexesInFiringArc(facing.Value, arc, weapon.Range.LongRange);
            weaponHexes.UnionWith(hexes);
        }

        // Filter out hexes not on the map to skip unnecessary LOS calculations
        if (game.BattleMap != null)
        {
            weaponHexes.RemoveWhere(hex => !game.BattleMap.IsOnMap(hex));
        }

        return new WeaponRangeAnalyzerExtensions.WeaponCandidate(weapon, weaponHexes, weapon.FirstMountPart.Level);
    }

    public bool IsHexInRange(HexCoordinates coordinates) =>
        _weaponRanges.Values.Any(range => range.Contains(coordinates));

    /// <summary>
    /// Gets all weapons that can fire at a given hex coordinate.
    /// </summary>
    public IReadOnlyList<Weapon> GetWeaponsInRange(HexCoordinates target) =>
        _weaponRanges
            .Where(kvp => kvp.Value.Contains(target))
            .Select(kvp => kvp.Key)
            .ToList();

    public bool IsWeaponInRange(Weapon weapon, HexCoordinates targetCoords) =>
        _weaponRanges.TryGetValue(weapon, out var range) &&
        range.Contains(targetCoords);

    /// <summary>
    /// Whether the weapon can fire at the selected unit. The hex highlight is an "any occupant"
    /// result, so a co-located grounded unit must not inherit the airborne unit's line of sight:
    /// once a target is selected, use that unit's own LOS result.
    /// </summary>
    public bool IsWeaponInRange(Weapon weapon, IUnit target, IUnit? attacker, IClientGame game)
    {
        if (target.Position == null || !IsWeaponInRange(weapon, target.Position.Coordinates))
            return false;

        if (game.BattleMap == null || attacker?.Position == null) return true;

        LineOfSightResult lineOfSight = game.BattleMap.GetLineOfSight(
            attacker.Position,
            target.Position,
            weapon.FirstMountPart?.Level ?? 0,
            target.Height);
        return lineOfSight.HasLineOfSight;
    }
}
