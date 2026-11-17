using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;
    public interface IMovementStep
    {
        MovementStep Step { get; }
        string ActionLabel { get; }
        void HandleMovementTypeSelection(MovementType movementType);
        void HandleHexSelection(Hex hex);
        void HandleFacingSelection(HexDirection direction);
        void ExecutePlayerAction();
    }

    public abstract class MovementStepBase : IMovementStep
    {
        protected readonly MovementState State;

        protected MovementStepBase(MovementState state)
        {
            State = state;
        }

        public abstract MovementStep Step { get; }
        public abstract string ActionLabel { get; }
        public virtual void HandleMovementTypeSelection(MovementType movementType) { }
        public virtual void HandleHexSelection(Hex hex) { }
        public virtual void HandleFacingSelection(HexDirection direction) { }
        public virtual void ExecutePlayerAction() { }
    }

    public sealed class SelectingUnitStep : MovementStepBase
    {
        public SelectingUnitStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingUnit;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectUnitToMove");
    }

    public sealed class SelectingMovementTypeStep : MovementStepBase
    {
        public SelectingMovementTypeStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingMovementType;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectMovementType");

        public override void HandleMovementTypeSelection(MovementType movementType)
        {
            if (!State.CanHumanPlayerAct()) return;
            if (State._selectedUnit?.Position == null) return;
            if (State.CurrentMovementStep != MovementStep.SelectingMovementType) return;

            if (movementType == MovementType.StandingStill)
            {
                // For standing still, we create an empty movement path
                var path = MovementPath.CreateSingleSegmentPath(State._selectedUnit.Position);
                State._builder.SetMovementPath(path);
                State.CompleteMovement();
                return;
            }

            State._selectedPath = new MovementPath([
                    new PathSegment(State._selectedUnit.Position, State._selectedUnit.Position, [])],
                movementType);
            State.HighlightReachableHexes();
            State.TransitionTo(new SelectingTargetHexStep(State));
        }
    }

    public sealed class SelectingTargetHexStep : MovementStepBase
    {
        public SelectingTargetHexStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingTargetHex;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectTargetHexOrEnemy");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandleTargetHexSelection(hex);
        }
    }

    public sealed class SelectingSurfaceStep : MovementStepBase
    {
        public HexCoordinates TargetHex { get; }

        public SelectingSurfaceStep(MovementState state, HexCoordinates targetHex, IReadOnlyList<HexReachabilityData> availableSurfaces) : base(state)
        {
            TargetHex = targetHex;

            var vm = new SurfaceSelectorViewModel(
                availableSurfaces,
                surface =>
                {
                    State._viewModel.HideSurfaceSelector();
                    State.HandleSurfaceSelection(surface);
                },
                State._viewModel.LocalizationService,
                () =>
                {
                    State._viewModel.HideSurfaceSelector();
                    State.TransitionTo(new SelectingTargetHexStep(State));
                });
            State._viewModel.ShowSurfaceSelector(targetHex, vm);
            State._viewModel.NotifyStateChanged();
        }
        public override MovementStep Step => MovementStep.SelectingSurface;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectSurface");
    }

    public sealed class SelectingDirectionStep : MovementStepBase
    {
        public SelectingDirectionStep(MovementState state, HexCoordinates targetHex) : base(state)
        {
            if (State._possibleDirections.Count == 0) return;
            State._viewModel.ShowDirectionSelector(targetHex, State._possibleDirections.Keys);
            State._viewModel.NotifyStateChanged();
        }
        public override MovementStep Step => MovementStep.SelectingDirection;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectFacingDirection");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandleTargetHexSelection(hex);
        }

        public override void HandleFacingSelection(HexDirection direction)
        {
            if (State.CurrentMovementStep != MovementStep.SelectingDirection) return;
            if (!State._possibleDirections.TryGetValue(direction, out var path)) return;

            State._selectedPath = State._selectedPath?.Append(path) ?? path;
            State._builder.SetMovementPath(State._selectedPath);

            State._viewModel.ShowDirectionSelector(path.Destination.Coordinates, [direction]);
            State._viewModel.ShowMovementPath(State._selectedPath);

            var movementType = State._selectedPath.MovementType;
            var remainingMp = State.GetRemainingMovementPoints();
            State.TransitionTo(new ConfirmMovementStep(State));
            if (movementType is MovementType.Walk or MovementType.Run && remainingMp > 0)
            {
                State.HighlightReachableHexes();
            }
            else
            {
                // Reachability is not recomputed from the new destination (a jump or a path that
                // used every MP), so the candidates and their highlights are stale.
                State.ClearMovementAttackHighlights();
            }
            State._viewModel.NotifyStateChanged();
        }
    }

    public sealed class ConfirmMovementStep : MovementStepBase
    {
        public ConfirmMovementStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.ConfirmMovement;
        public override string ActionLabel => GetActionLabel();

        private string GetActionLabel()
        {
            var remainingMp = State.GetRemainingMovementPoints();
            var movementType = State._selectedPath?.MovementType;

            // Check if we can continue movement (walk/run with remaining MPs)
            if (movementType is MovementType.Walk or MovementType.Run && remainingMp > 0)
            {
                return State._viewModel.LocalizationService.GetString("Action_ConfirmOrSelectNextHex");
            }

            // No more movement possible, just confirm
            return State._viewModel.LocalizationService.GetString("Action_ConfirmMovement");
        }

        public override void HandleHexSelection(Hex hex)
        {
            // An enemy hex can open a Charge/DFA declaration even in the confirmation step.
            if (State.TryHandleEnemyHex(hex)) return;

            // Reset selection if clicked outside reachable hexes during the confirmation step
            // This matches the behavior in HandleTargetHexSelection and SelectingDirectionStep
            if (State._reachabilityData != null && !State._reachabilityData.Value.IsHexReachable(hex.Coordinates))
            {
                State.ResetUnitSelection();
                return;
            }

            if (State._selectedPath?.MovementType is not (MovementType.Walk or MovementType.Run)) return;
            if (State.GetRemainingMovementPoints() <= 0) return;
            State.HandleTargetHexSelection(hex);
        }

        public override void HandleFacingSelection(HexDirection direction)
        {
            State.ConfirmMovement();
        }

        public override void ExecutePlayerAction()
        {
            State.ConfirmMovement();
        }
    }

    public sealed class ConfirmMovementAttackStep : MovementStepBase
    {
        public ConfirmMovementAttackStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.ConfirmMovementAttack;
        public override string ActionLabel =>
            State._viewModel.LocalizationService.GetString("Action_ConfirmMovementAttack");

        public override void HandleHexSelection(Hex hex)
        {
            State.HandlePendingMovementAttackHexSelection(hex);
        }

        public override void ExecutePlayerAction()
        {
            State.ConfirmMovementAttack();
        }
    }

    public sealed class SelectingStandingUpDirectionStep : MovementStepBase
    {
        public SelectingStandingUpDirectionStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.SelectingStandingUpDirection;
        public override string ActionLabel => State._viewModel.LocalizationService.GetString("Action_SelectFacingDirection");

        public override void HandleFacingSelection(HexDirection direction)
        {
            State.CompleteStandupAttempt(direction);
        }
    }

    public sealed class CompletedStep : MovementStepBase
    {
        public CompletedStep(MovementState state) : base(state) { }
        public override MovementStep Step => MovementStep.Completed;
        public override string ActionLabel => string.Empty;
    }
