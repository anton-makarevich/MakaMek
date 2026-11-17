using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Movement-attack (charge/DFA) candidate search and declaration handling, expressed as extensions
/// over <see cref="MovementState"/>.
/// </summary>
public static class MovementAttackExtensions
{
    extension(MovementState state)
    {

    public void UpdateMovementAttackCandidates(MovementType movementType, int remainingMp)
    {
        state._movementAttackCandidates = state.FindMovementAttackCandidates(movementType, remainingMp);
        if (state._movementAttackCandidates.Count == 0) return;

        var attackType = movementType.GetMovementAttackType();
        if (attackType is null) return;

        var coordinates = state._movementAttackCandidates.Keys
            .Where(unit => unit.Position != null)
            .Select(unit => unit.Position!.Coordinates)
            .ToHashSet();
        if (coordinates.Count == 0) return;

        var label = state.GetString($"PhysicalAttackType_{attackType}");
        state._viewModel.HighlightCoordinates(coordinates, new AttackReachableHighlight([label]));
    }

    public IReadOnlyDictionary<IUnit, MovementAttackCandidate> FindMovementAttackCandidates(
        MovementType movementType,
        int remainingMp)
    {
        var candidates = new Dictionary<IUnit, MovementAttackCandidate>();
        if (state._selectedUnit == null
            || state._selectedPath == null
            || state.Game?.BattleMap is not { } map
            || state.Game.PhaseStepState?.ActivePlayer is null
            || movementType.GetMovementAttackType() is not { } attackType)
            return candidates;

        var allUnits = state.GetAllUnits();
        // The target rule belongs to the movement attack validator; this method only adds the UI
        // concern of picking the cheapest legal path to each target it reports.
        var potentialTargets = state.Game.MovementAttackValidator
            .GetPotentialTargets(state._selectedUnit, movementType, allUnits);
        var startPosition = state._selectedPath.Destination;
        var canMoveBackward = state._selectedUnit.CanMoveBackward(movementType);

        foreach (var target in potentialTargets)
        {
            var cheapest = state.FindCheapestAttackPath(
                target, attackType, movementType, remainingMp, allUnits, map,
                startPosition, canMoveBackward);
            if (cheapest != null) candidates[target] = cheapest;
        }

        return candidates;
    }

    /// <summary>
    /// The cheapest path from the current position into the target's hex that the shared
    /// declaration validator accepts, or null when no such path exists.
    /// </summary>
    public MovementAttackCandidate? FindCheapestAttackPath(
        IUnit target,
        PhysicalAttackType attackType,
        MovementType movementType,
        int remainingMp,
        IReadOnlyList<IUnit> allUnits,
        IBattleMap map,
        HexPosition startPosition,
        bool canMoveBackward)
    {
        var selectedUnit = state._selectedUnit!;
        var selectedPath = state._selectedPath!;
        var targetCoordinates = target.Position!.Coordinates;
        var prohibited = state._prohibitedHexes
            .Where(coordinates => coordinates != targetCoordinates)
            .ToHashSet();

        var reachability = map.GetReachableHexesForPosition(
            startPosition,
            remainingMp,
            canMoveBackward,
            movementType,
            prohibited,
            state._friendlyUnitsCoordinates,
            selectedUnit.Height,
            selectedUnit.MaxLevelChangeForward,
            selectedUnit.MaxLevelChangeBackward);

        var paths = map.GetPathsToHexWithAllFacings(
            startPosition,
            targetCoordinates,
            movementType,
            remainingMp,
            reachability,
            selectedUnit.Height,
            selectedUnit.MaxLevelChangeForward,
            selectedUnit.MaxLevelChangeBackward,
            prohibited,
            targetSurface: target.Position.Surface,
            approachWaypointOffset: state.Game!.RulesProvider.GetDfaAirborneBaseOffset());

        MovementAttackCandidate? cheapest = null;
        foreach (var path in paths.Values)
        {
            var declaration = new PhysicalAttackDeclarationData
            {
                AttackType = attackType,
                AttackerLimbs = [],
                TargetId = target.Id
            };

            var validation = state.Game.MovementAttackValidator.Validate(
                selectedUnit,
                selectedPath.Append(path).ToData(),
                movementType,
                declaration,
                allUnits,
                map);
            if (!validation.IsValid || validation.Declaration is not { } validated) continue;

            if (cheapest == null || path.TotalCost < cheapest.Path.TotalCost)
                cheapest = new MovementAttackCandidate(target, path, validated);
        }

        return cheapest;
    }

    }
}
