using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Eligibility-backed view of the physical-attack battlefield: which units the attacker can reach,
/// which units may act as attackers, and the target highlights. Kept out of
/// <see cref="PhysicalAttackState"/> so the state only orchestrates the selection steps.
/// </summary>
public sealed class PhysicalAttackTargeting
{
    private static readonly List<PhysicalAttackType> SupportedAttackTypes =
        PhysicalAttackDefinitions.Supported.Select(definition => definition.AttackType).ToList();

    private HashSet<IUnit> _reachableTargets = [];

    public bool IsReachable(IUnit unit) => _reachableTargets.Contains(unit);

    public void Reset() => _reachableTargets = [];

    /// <summary>
    /// Recomputes the units the attacker can reach with at least one supported attack type. The
    /// eligibility policy filters enemies, destroyed units and targets without a usable limb.
    /// </summary>
    public void Rebuild(IUnit? attacker, IClientGame game, IEnumerable<IUnit> units)
    {
        var reachable = new HashSet<IUnit>();
        if (attacker is { } acting && game.BattleMap is { } map)
        {
            // The eligibility policy filters enemies, destroyed units and targets it cannot reach;
            // the UI only collects the candidates that report a usable limb.
            foreach (var candidate in units)
            {
                if (SupportedAttackTypes.Any(attackType =>
                        game.PhysicalAttackEligibility
                            .GetEligibleLimbs(acting, candidate, attackType, map).Count > 0))
                    reachable.Add(candidate);
            }
        }

        _reachableTargets = reachable;
    }

    public static IUnit? FindUnitOnHex(IEnumerable<IUnit> units, Hex hex) =>
        units.FirstOrDefault(unit => unit.Position?.Coordinates == hex.Coordinates);

    /// <summary>
    /// Highlights every hex that holds a unit the attacker can reach with any supported attack type.
    /// The labels are the localized attack type names, matching the weapon reach highlight
    /// convention.
    /// </summary>
    public void Highlight(
        IUnit? attacker,
        IClientGame game,
        BattleMapViewModel viewModel,
        Func<string, string> getString)
    {
        if (attacker is not { } acting || game.BattleMap is not { } map) return;

        var hexHighlights = new Dictionary<HexCoordinates, HashSet<string>>();

        foreach (var enemy in _reachableTargets)
        {
            var labels = new HashSet<string>();
            foreach (var attackType in SupportedAttackTypes)
            {
                if (game.PhysicalAttackEligibility.GetEligibleLimbs(acting, enemy, attackType, map).Count == 0)
                    continue;

                labels.Add(getString($"PhysicalAttackType_{attackType}"));
            }

            if (labels.Count == 0) continue;
            hexHighlights[enemy.Position!.Coordinates] = labels;
        }

        if (hexHighlights.Count == 0)
        {
            viewModel.ClearHighlights();
            return;
        }

        var highlights = new Dictionary<HexCoordinates, IHexHighlightType>(hexHighlights.Count);
        foreach (var (coordinates, labels) in hexHighlights)
            highlights[coordinates] = new AttackReachableHighlight([
                .. labels.OrderBy(label => label, StringComparer.InvariantCulture)
            ]);

        viewModel.HighlightRegions(highlights);
    }
}
