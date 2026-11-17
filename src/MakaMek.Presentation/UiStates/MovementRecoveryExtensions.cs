using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Stand-up and fall recovery flow, expressed as extensions over <see cref="MovementState"/>.
/// </summary>
public static class MovementRecoveryExtensions
{
    extension(MovementState state)
    {
    public void AttemptStandup(MovementType movementType)
    {
        lock (state._stateLock)
        {
            if (state._viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
            if (state._selectedUnit?.Position == null) return;

            // Set the selected movement type for later use
            state._selectedPath = new MovementPath([
                new PathSegment(state._selectedUnit.Position, state._selectedUnit.Position, [])],
                movementType);
            // Ensure the builder has the unit and movement type set
            state._builder.SetUnit(state._selectedUnit);
            state._builder.SetMovementPath(state._selectedPath);

            state.TransitionTo(new SelectingStandingUpDirectionStep(state));
            state._viewModel.ShowDirectionSelector(state._selectedUnit.Position.Coordinates, Enum.GetValues<HexDirection>());
            state._viewModel.NotifyStateChanged();
        }
    }

    public void CompleteStandupAttempt(HexDirection direction)
    {
        lock (state._stateLock)
        {
            if (state._viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
            if (state._selectedUnit?.Position == null) return;
            if (state._selectedPath?.MovementType == null) return;

            // Create a standup command with the selected direction
            var standupCommand = new TryStandupCommand
            {
                GameOriginId = state._viewModel.Game.Id,
                UnitId = state._selectedUnit.Id,
                PlayerId = state._viewModel.Game.PhaseStepState.Value.ActivePlayer.Id,
                NewFacing = direction,
                MovementTypeAfterStandup = state._selectedPath.MovementType
            };

            // Hide the selectors; the movement path context is kept so that a fall from
            // this failed standup attempt can resume movement (see state.ResumeMovementAfterFall)
            state._viewModel.HideDirectionSelector();
            state._viewModel.HideSurfaceSelector();
            state._viewModel.NotifyStateChanged();

            // Publish the command
            state._viewModel.Game.TryStandupUnit(standupCommand);
        }
    }

    public bool TryAdoptFallenUnit(Guid unitId)
    {
        var activePlayerId = state.Game?.PhaseStepState?.ActivePlayer.Id;

        if (state._selectedUnit == null)
        {
            var fallenUnit = state._viewModel.Units.FirstOrDefault(u => u.Id == unitId);
            if (fallenUnit == null)
            {
                state.Game?.Logger.LogWarning(
                    "state.ResumeMovementAfterFall: unit {UnitId} not found among alive units", unitId);
                return false;
            }

            if (fallenUnit.Owner?.Id != activePlayerId)
            {
                state.Game?.Logger.LogWarning(
                    "Resume movement after fall ignored: unit {UnitId} is not controlled by the active player", unitId);
                return false;
            }

            state._selectedUnit = fallenUnit;
            state._builder.SetUnit(fallenUnit);
        }
        else if (state._selectedUnit.Id != unitId)
        {
            state.Game?.Logger.LogWarning(
                "Resume movement after fall ignored: command unit {CommandUnit} does not match movement state's selected unit {StateUnit}",
                unitId,
                state._selectedUnit.Id);
            return false;
        }

        if (state._selectedUnit.Owner?.Id == activePlayerId) return true;

        state.Game?.Logger.LogWarning(
            "Resume movement after fall ignored: unit {UnitId} is not controlled by the active player", unitId);
        return false;
    }

    public void CompleteMovementAfterFailedFall(Mech mech, Guid unitId)
    {
        state.Game?.Logger.LogWarning(
            "Resume movement after fall: unit {UnitId} is {Reason}, ending movement",
            unitId,
            mech.Position == null ? "not positioned" : "not prone");

        if (mech.Position != null)
        {
            state._builder.SetUnit(state._selectedUnit);
            state._builder.SetMovementPath(MovementPath.CreateSingleSegmentPath(mech.Position));
        }

        state.CompleteMovement();
    }

    public MovementPath RebuildPathAtCurrentPosition(Mech mech, Guid unitId)
    {
        if (state._selectedPath != null)
            return MovementPath.CreateSingleSegmentPath(mech.Position!, state._selectedPath.MovementType);

        state.Game?.Logger.LogWarning(
            "Resume movement after fall: no movement path in state for unit {UnitId}, rebuilding at current position",
            unitId);

        return MovementPath.CreateSingleSegmentPath(
            mech.Position!,
            mech.MovementTaken?.MovementType ?? MovementType.Walk);
    }

    public void HandleProneFacingChange(Mech mech)
    {
        if (state._viewModel.Game?.PhaseStepState?.ActivePlayer == null) return;
        if (mech.Position == null || !mech.IsProne) return;

        state._movementPoints = mech.GetMovementPoints(MovementType.Walk);

        // Calculate maximum rotation steps based on available movement points
        var maxRotateSteps = Math.Min(3, state._movementPoints);

        // Reset possible directions
        state._possibleDirections = [];

        var currentFacing = mech.Facing;
        if (currentFacing == null) return;

        state._selectedPath = new MovementPath([
            new PathSegment(mech.Position, mech.Position, [])],
            MovementType.Walk);
        state._builder.SetMovementPath(state._selectedPath);

        void AddToPossibleDirections(HexDirection direction, int cost)
        {
            var pathSegments = new MovementPath([
                new PathSegment(mech.Position, mech.Position with { Facing = direction }, [new RotationMovementCost { FromFacing = mech.Position.Facing, ToFacing = direction, Value = cost }])
            ], MovementType.Walk);
            state._possibleDirections[direction] = pathSegments;
        }

        // Generate possible directions by rotating from the current facing
        for (var steps = 1; steps <= maxRotateSteps; steps++)
        {
            var rotatedDirectionCw = currentFacing.Value.Rotate(steps);
            AddToPossibleDirections(rotatedDirectionCw, steps);

            if (steps == 3) break; // We don't want to duplicate an opposite direction

            var rotatedDirectionCcw = currentFacing.Value.Rotate(-steps);
            AddToPossibleDirections(rotatedDirectionCcw, steps);
        }

        state.TransitionTo(new SelectingDirectionStep(state, mech.Position.Coordinates));
    }

    public void ResumeMovementAfterStandup(Guid unitId)
    {
        lock (state._stateLock)
        {
            // Fallback: if state._selectedUnit is null (e.g., cleared by ClearSelection during phase change),
            // try to find the unit from the view model
            if (state._selectedUnit == null || state._selectedUnit.Id != unitId)
            {
                var stoodUpUnit = state._viewModel.Units.FirstOrDefault(u => u.Id == unitId);
                if (stoodUpUnit == null)
                {
                    state.Game?.Logger.LogWarning("state.ResumeMovementAfterStandup: unit {UnitId} not found among alive units", unitId);
                    return;
                }
                state._selectedUnit = stoodUpUnit;
                state._builder.SetUnit(stoodUpUnit);
            }

            // Check if the unit is no longer prone (standup was successful)
            if (state._selectedPath?.MovementType == null ||
                state._selectedUnit is not Mech { IsProne: false, Position: not null } mech) return;

            if (mech.GetMovementPoints(state._selectedPath.MovementType) < 1)
            {
                // No more movement possible, just confirm
                state.CompleteMovement();
                return;
            }

            state._isPostStandupMovement = true; // Mark that this unit is in post-standup movement state
            state._selectedPath = new MovementPath([
                new PathSegment(mech.Position, mech.Position, [])],
                state._selectedPath.MovementType);
            state._builder.SetMovementPath(state._selectedPath);
            state.HighlightReachableHexes();
            state.TransitionTo(new SelectingTargetHexStep(state));
        }
    }

    public void ResumeMovementAfterFall(Guid unitId)
    {
        lock (state._stateLock)
        {
            if (!state.TryAdoptFallenUnit(unitId)) return;

            if (state._selectedUnit is not Mech mech)
            {
                state.Game?.Logger.LogWarning(
                    "Resume movement after fall ignored: unit {UnitId} is not a mech", unitId);
                return;
            }

            if (mech.Position == null || !mech.IsProne)
            {
                state.CompleteMovementAfterFailedFall(mech, unitId);
                return;
            }

            state._selectedPath = state.RebuildPathAtCurrentPosition(mech, unitId);

            if (!mech.CanStandup())
            {
                state._builder.SetUnit(state._selectedUnit);
                state._builder.SetMovementPath(state._selectedPath);
                state.CompleteMovement();
                return;
            }

            state._deferredMovementUnitId = unitId;

            state.TransitionTo(new SelectingMovementTypeStep(state));
            state._viewModel.NotifySelectedUnitChanged();
        }
    }
    }
}
