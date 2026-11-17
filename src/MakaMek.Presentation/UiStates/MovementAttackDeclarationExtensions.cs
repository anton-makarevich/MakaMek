using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Charge/DFA declaration open/confirm/cancel handling, expressed as extensions over
/// <see cref="MovementState"/>.
/// </summary>
public static class MovementAttackDeclarationExtensions
{
    extension(MovementState state)
    {
    public void OpenMovementAttackDeclaration(MovementAttackCandidate candidate)
    {
        if (state._selectedUnit == null
            || state._selectedUnit.Pilot is null
            || state._selectedPath == null
            || state._selectedPath.Destination.Coordinates != candidate.Path.Start.Coordinates
            || state.Game?.BattleMap is not { } map) return;

        var path = state._selectedPath.Append(candidate.Path);
        var declaration = candidate.Declaration;
        var stopPosition = path.GetStopPosition(declaration) ?? path.Destination;
        if (declaration.AttackType == PhysicalAttackType.DFA)
        {
            // Mirror BaseGame.OnMoveUnit: the attacker waits airborne above the approach hex.
            stopPosition = stopPosition with
            {
                AirborneLevel = map.GetDfaAirborneBaseLevel(
                    stopPosition,
                    path.Destination,
                    state.Game.RulesProvider.GetDfaAirborneBaseOffset())
            };
        }

        var scenario = PhysicalAttackScenario.ForPreview(
            state._selectedUnit,
            candidate.Target,
            declaration.AttackType,
            stopPosition,
            path.MovementType,
            candidate.Target.MovementTaken?.HexesTraveled ?? 0);
        var breakdown = state.Game.PhysicalAttackCalculator.GetMovementAttackModifierBreakdown(scenario, map);
        var descriptionLines = state.BuildMovementAttackDescription(candidate.Target, declaration);
        var option = new PhysicalAttackOptionViewModel(
            declaration.AttackType,
            [],
            true,
            null,
            breakdown.Total,
            breakdown,
            (_, _) => { },
            state._viewModel.LocalizationService,
            descriptionLines);

        state._pendingAttackTarget = candidate.Target;
        state._pendingAttackPath = path;
        state._pendingAttackDeclaration = declaration;
        state._pendingAttackOptions = [option];

        state._viewModel.HideDirectionSelector();
        state._viewModel.HideSurfaceSelector();
        state._viewModel.ShowMovementPath(path);
        state.TransitionTo(new ConfirmMovementAttackStep(state));
    }

    public void ClearPendingMovementAttack()
    {
        state._pendingAttackTarget = null;
        state._pendingAttackPath = null;
        state._pendingAttackDeclaration = null;
        state._pendingAttackOptions = MovementState.NoAttackOptions;
    }

    public void ConfirmMovementAttack()
    {
        if (!state.CanHumanPlayerAct()) return;

        var target = state._pendingAttackTarget;
        var path = state._pendingAttackPath;
        var declaration = state._pendingAttackDeclaration;
        if (state._selectedUnit == null
            || target == null
            || path == null
            || declaration == null
            || state.Game?.BattleMap is not { } map)
        {
            state.CancelPendingMovementAttack();
            return;
        }

        var validation = state.Game.MovementAttackValidator.Validate(
            state._selectedUnit,
            path.ToData(),
            path.MovementType,
            declaration,
            state.GetAllUnits(),
            map);
        if (!validation.IsValid)
        {
            state.CancelPendingMovementAttack();
            return;
        }

        state._builder.SetMovementPath(path);
        state._builder.SetMovementAttackTarget(target);
        state.CompleteMovement();
    }

    public void CancelPendingMovementAttack()
    {
        state.ClearPendingMovementAttack();
        state._viewModel.ShowMovementPath(state._selectedPath);

        if (state._selectedPath is { TotalCost: > 0 })
        {
            // The partial move is still active. Restore the confirmation step together with the
            // single-direction selector it showed before the declaration opened. The declaration
            // can be opened from a direction selection for a different, unconfirmed hex, so the
            // dictionary must only hold the partial move: a stale entry for another hex would make
            // ConfirmMovement replace the partial move with a standing-still path.
            state._possibleDirections = new Dictionary<HexDirection, MovementPath>
            {
                [state._selectedPath.Destination.Facing] = state._selectedPath
            };
            state._viewModel.ShowDirectionSelector(
                state._selectedPath.Destination.Coordinates,
                [state._selectedPath.Destination.Facing]);
            state.TransitionTo(new ConfirmMovementStep(state));
        }
        else
        {
            state.TransitionTo(new SelectingTargetHexStep(state));
        }

        state.HighlightReachableHexes();
    }
    }
}
