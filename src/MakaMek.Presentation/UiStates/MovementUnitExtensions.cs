using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Unit selection and the small shared lookups used by the movement steps, expressed as extensions
/// over <see cref="MovementState"/> so the state stays a small orchestration facade.
/// </summary>
public static class MovementUnitExtensions
{
    extension(MovementState state)
    {
        public IMovementStep CreateInitialStep() => new SelectingUnitStep(state);

        public bool ResolveCanSelectUnit(IUnit? unit)
        {
            if (state._deferredMovementUnitId is not { } lockedId) return true;
            if (unit == null) return true;
            return unit.Id == lockedId;
        }

        public void ApplyUnitSelectionFromList(IUnit? unit)
        {
            if (unit != null && state._selectedUnit != null && state._selectedUnit != unit)
            {
                state.ResetUnitSelection();
            }

            state.SelectedUnit = unit;
        }

        public void ApplySelectedUnit(IUnit? value)
        {
            lock (state._stateLock)
            {
                if (!state.CanHumanPlayerAct()) return;
                // A pending Charge/DFA declaration owns the unit until it is confirmed or cancelled.
                if (state._pendingAttackTarget != null) return;
                if (value == null)
                {
                    state.ResetUnitSelection();
                    return;
                }

                if (!state.CanSelectUnit(value)) return;

                if (value.Status == UnitStatus.Destroyed) return;
                if (value.HasMoved && value.Id != state._deferredMovementUnitId) return;

                state._selectedUnit = value;
                state._builder.SetUnit(value);
                state._isPostStandupMovement = false;
                state.TransitionTo(new SelectingMovementTypeStep(state));
                state._viewModel.NotifySelectedUnitChanged();
            }
        }

    internal List<IUnit> GetAllUnits() =>
            state.Game?.Players.SelectMany(player => player.Units).ToList() ?? [];

        internal string GetString(string key) => state._viewModel.LocalizationService.GetString(key);

    public void CompleteMovement()
    {
        lock (state._stateLock)
        {
            state._deferredMovementUnitId = null;
            state.ClearPendingMovementAttack();

            state.ClearHighlighting();
            state._selectedUnit = null;
            state._viewModel.HideMovementPath();
            state._viewModel.HideDirectionSelector();
            state._viewModel.HideSurfaceSelector();
            state._viewModel.NotifySelectedUnitChanged();
            state._isPostStandupMovement = false; // Reset post-standup state when movement is completed
            state.TransitionTo(new CompletedStep(state));

            var command = state._builder.Build();
            if (command != null && state._viewModel.Game is { } clientGame)
            {
                state._builder.Reset();
                clientGame.MoveUnit(command.Value);
            }
        }
    }
    }
}
