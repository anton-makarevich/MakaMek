using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Movement selection and movement-attack behaviour (reachability, facing paths, charge/DFA
/// declarations), expressed as extensions over <see cref="MovementState"/> so the state stays a small
/// orchestration facade.
/// </summary>
public static class MovementSelectionExtensions
{
    extension(MovementState state)
    {

    public void ClearHighlighting()
    {
        if (state._reachabilityData != null)
            state._viewModel.RemoveHighlight<MovementReachableHighlight>(state._reachabilityData.Value.AllReachableCoordinates);
        state._reachabilityData = null;
        state.ClearMovementAttackHighlights();
    }

    public void HighlightReachableHexes()
    {
        if (state._selectedPath?.MovementType == null) return;
        var movementType = state._selectedPath.MovementType;

        state.ClearHighlighting();

        var remainingMp = state.GetRemainingMovementPoints();
        state._movementPoints = remainingMp;

        var battleMap = state._viewModel.Game?.BattleMap;
        if (state._selectedUnit != null && battleMap != null)
        {
            var startPosition = state._selectedPath.Destination;
            state._reachabilityData = battleMap.GetReachableHexesForPosition(
                startPosition,
                remainingMp,
                state._selectedUnit.CanMoveBackward(movementType),
                movementType,
                state._prohibitedHexes,
                state._friendlyUnitsCoordinates,
                state._selectedUnit.Height,
                state._selectedUnit.MaxLevelChangeForward,
                state._selectedUnit.MaxLevelChangeBackward);

            state._viewModel.HighlightCoordinates(state._reachabilityData.Value.AllReachableCoordinates, new MovementReachableHighlight(movementType));

            state.UpdateMovementAttackCandidates(movementType, remainingMp);
        }

        state._viewModel.NotifyStateChanged();
    }

    public int GetRemainingMovementPoints()
    {
        if (state._selectedPath == null || state._selectedUnit == null) return 0;
        return Math.Max(0, state._selectedUnit.GetMovementPoints(state._selectedPath.MovementType) - state._selectedPath.TotalCost);
    }

    public void ConfirmMovement()
    {
        if (state.CurrentMovementStep != MovementStep.ConfirmMovement) return;
        var direction = state._viewModel.AvailableDirections?.FirstOrDefault();
        if (direction == null || !state._possibleDirections.TryGetValue(direction.Value, out var path))
        {
            // A cancelled movement-attack declaration restores the confirmation step after the
            // direction selector is gone; the path built so far is the move to confirm.
            if (state._selectedPath is not { TotalCost: > 0 } pathSoFar) return;
            state._builder.SetMovementPath(pathSoFar);
            state.CompleteMovement();
            return;
        }

        if (state._viewModel.MovementPath == null || state._viewModel.MovementPath.Last().To != path.Destination)
        {
            if (state._selectedUnit?.Position == null) return;
            path = MovementPath.CreateSingleSegmentPath(state._selectedUnit.Position);
            state._builder.SetMovementPath(path);
        }

        state.CompleteMovement();
    }

    public bool HandleUnitSelectionFromHex(Hex hex)
    {
        var unit = state._viewModel.Units.FirstOrDefault(u => u.Position?.Coordinates == hex.Coordinates);
        if (unit == null) return false;

        if (unit == state._selectedUnit)
        {
            if (state.CurrentMovementStep != MovementStep.SelectingUnit) return false;
            state.SelectedUnit = null;
            return true;
        }

        if (unit.Owner?.Id != state._viewModel.Game?.PhaseStepState?.ActivePlayer.Id) return false;
        if (!state.CanSelectUnit(unit)) return false;

        state.SelectedUnit = null;
        state.SelectedUnit = unit;
        return true;
    }

    public void ResetUnitSelection()
    {
        if (state._isPostStandupMovement) return;
        lock (state._stateLock)
        {
            if (state._selectedUnit == null) return;
            state._selectedUnit = null;
            state._builder.Reset();
            state.ClearPendingMovementAttack();
            state._viewModel.HideMovementPath();
            state._viewModel.HideDirectionSelector();
            state._viewModel.HideSurfaceSelector();
            state.ClearHighlighting();
            state.TransitionTo(new SelectingUnitStep(state));
            state._viewModel.NotifySelectedUnitChanged();
        }
    }

    public void HandleTargetHexSelection(Hex hex)
    {
        if (state._selectedUnit?.Position == null
            || state._selectedPath?.MovementType == null
            || state.Game == null
            || state._reachabilityData == null) return;

        // An enemy hex is never reachable. If it can be legally charged or DFA'd it opens the
        // declaration; an ineligible enemy is ignored instead of cancelling the move.
        if (state.TryHandleEnemyHex(hex)) return;

        // Reset selection if clicked outside reachable hexes during target hex selection
        // BUT NOT if the unit is in post-standup movement state (must complete declared movement)
        if (!state._reachabilityData.Value.IsHexReachable(hex.Coordinates))
        {
            state.ResetUnitSelection();
            return;
        }

        var availableSurfaces = state._reachabilityData.Value
            .GetReachableSurfacesForCoordinate(hex.Coordinates)
            .DistinctBy(s => s.Surface)
            .ToList();

        // If only one surface, proceed directly
        if (availableSurfaces.Count == 1)
        {
            var surface = availableSurfaces[0].Surface;
            state._movementPoints = state.GetRemainingMovementPoints();
            state.BuildPathsForSurfaceSelection(hex, surface);
            return;
        }

        // Multiple surfaces - pause for user selection
        state.TransitionTo(new SelectingSurfaceStep(state, hex.Coordinates, availableSurfaces));
    }

    public void BuildPathsForSurfaceSelection(Hex hex, HexSurface surface)
    {
        var startPosition = state._selectedPath!.Destination;
        state._movementPoints = state.GetRemainingMovementPoints();
        state._possibleDirections = state.Game!.BattleMap?.GetPathsToHexWithAllFacings(
            startPosition,
            hex.Coordinates,
            state._selectedPath!.MovementType,
            state._movementPoints,
            state._reachabilityData!.Value,
            state._selectedUnit!.Height,
            state._selectedUnit.MaxLevelChangeForward,
            state._selectedUnit.MaxLevelChangeBackward,
            state._prohibitedHexes,
            targetSurface: surface,
            approachWaypointOffset: state.Game!.RulesProvider.GetDfaAirborneBaseOffset()) ?? [];

        state.TransitionTo(new SelectingDirectionStep(state, hex.Coordinates));
    }

    public void HandleSurfaceSelection(HexSurface surface)
    {
        var targetHex = (state._step as SelectingSurfaceStep)?.TargetHex;
        if (targetHex == null) return;

        state._movementPoints = state.GetRemainingMovementPoints();
        var startPosition = state._selectedPath!.Destination;
        state._possibleDirections = state.Game!.BattleMap?.GetPathsToHexWithAllFacings(
            startPosition,
            targetHex,
            state._selectedPath!.MovementType,
            state._movementPoints,
            state._reachabilityData!.Value,
            state._selectedUnit!.Height,
            state._selectedUnit.MaxLevelChangeForward,
            state._selectedUnit.MaxLevelChangeBackward,
            state._prohibitedHexes,
            targetSurface: surface,
            approachWaypointOffset: state.Game!.RulesProvider.GetDfaAirborneBaseOffset()) ?? [];

        state.TransitionTo(new SelectingDirectionStep(state, targetHex));
    }

    }
}
