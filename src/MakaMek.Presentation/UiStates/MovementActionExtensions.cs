using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Movement actions and the stand-up/fall recovery flow, expressed as extensions over
/// <see cref="MovementState"/> so the state stays a small orchestration facade.
/// </summary>
public static class MovementActionExtensions
{
    extension(MovementState state)
    {
    public string GetPsrProbabilityText(Mech mech, PilotingSkillRollType rollType)
    {
        var psrBreakdown = state._viewModel.Game!.PilotingSkillCalculator.GetPsrBreakdown(
            mech, new PilotingSkillRollContext(rollType));
        var probability = Core.Utils.DiceUtils.Calculate2d6Probability(psrBreakdown.ModifiedPilotingSkill);
        return $" ({probability:0}%)";
    }

    public StateAction CreateStayProneAction()
    {
        return new StateAction(
            state._viewModel.LocalizationService.GetString("Action_StayProne"),
            true,
            () => state.HandleMovementTypeSelection(MovementType.StandingStill));
    }

    public StateAction CreateProneFacingChangeAction(Mech mech)
    {
        var availableMp = mech.GetMovementPoints(MovementType.Walk);
        return new StateAction(
            string.Format(state._viewModel.LocalizationService.GetString("Action_ChangeFacing"), availableMp),
            true,
            () => state.HandleProneFacingChange(mech));
    }

    public string GetStandupActionText(MovementType movementType, string probabilityText)
    {
        if (state._selectedUnit is Mech { IsMinimumMovement: true })
            return state._viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText;

        var typeKey = movementType == MovementType.Run ? "MovementType_Run" : "MovementType_Walk";
        return string.Format(
            state._viewModel.LocalizationService.GetString("Action_MovementPoints"),
            state._viewModel.LocalizationService.GetString(typeKey),
            state._selectedUnit!.GetMovementPoints(movementType)) + probabilityText;
    }

    public List<StateAction> GetLockedProneActions(Mech mech)
    {
        var lockedType = mech.MovementTaken!.MovementType;
        var lockedProneActions = new List<StateAction>
        {
            state.CreateStayProneAction()
        };

        if (!mech.CanStandup()) return lockedProneActions;

        var probabilityText = state.GetPsrProbabilityText(mech, PilotingSkillRollType.StandupAttempt);
        lockedProneActions.Add(new StateAction(
            state.GetStandupActionText(lockedType, probabilityText),
            true,
            () => state.AttemptStandup(lockedType)));

        if (mech.CanChangeFacingWhileProne())
            lockedProneActions.Add(state.CreateProneFacingChangeAction(mech));

        return lockedProneActions;
    }

    public List<StateAction> GetStartOfPhaseProneActions(Mech mech)
    {
        var proneActions = new List<StateAction>
        {
            state.CreateStayProneAction()
        };

        if (mech.IsImmobile) return proneActions;

        if (mech.CanStandup())
        {
            var probabilityText = state.GetPsrProbabilityText(mech, PilotingSkillRollType.StandupAttempt);

            if (mech.IsMinimumMovement)
            {
                proneActions.Add(new StateAction(
                    state._viewModel.LocalizationService.GetString("Action_AttemptStandup") + probabilityText,
                    true,
                    () => state.AttemptStandup(MovementType.Walk)));
            }
            else
            {
                proneActions.Add(new StateAction(
                    state.GetStandupActionText(MovementType.Walk, probabilityText),
                    true,
                    () => state.AttemptStandup(MovementType.Walk)));

                if (mech.CanRun)
                {
                    proneActions.Add(new StateAction(
                        state.GetStandupActionText(MovementType.Run, probabilityText),
                        true,
                        () => state.AttemptStandup(MovementType.Run)));
                }
            }
        }

        if (mech.CanChangeFacingWhileProne())
            proneActions.Add(state.CreateProneFacingChangeAction(mech));

        return proneActions;
    }

    public List<StateAction> GetNormalActions()
    {
        var actions = new List<StateAction>
        {
            new(
                state._viewModel.LocalizationService.GetString("Action_StandStill"),
                true,
                () => state.HandleMovementTypeSelection(MovementType.StandingStill))
        };

        if (state._selectedUnit!.IsImmobile) return actions;

        actions.Add(new StateAction(
            string.Format(state._viewModel.LocalizationService.GetString("Action_MovementPoints"),
                state._viewModel.LocalizationService.GetString("MovementType_Walk"),
                state._selectedUnit.GetMovementPoints(MovementType.Walk)),
            true,
            () => state.HandleMovementTypeSelection(MovementType.Walk)));

        if (state._selectedUnit is Mech { CanRun: true })
        {
            actions.Add(new StateAction(
                string.Format(state._viewModel.LocalizationService.GetString("Action_MovementPoints"),
                    state._viewModel.LocalizationService.GetString("MovementType_Run"),
                    state._selectedUnit.GetMovementPoints(MovementType.Run)),
                true,
                () => state.HandleMovementTypeSelection(MovementType.Run)));
        }

        if (state._selectedUnit is not Mech { CanJump: true }) return actions;
        var jumpPoints = state._selectedUnit.GetMovementPoints(MovementType.Jump);

        var jumpActionText = string.Format(state._viewModel.LocalizationService.GetString("Action_MovementPoints"),
            state._viewModel.LocalizationService.GetString("MovementType_Jump"),
            jumpPoints);

        if (state._selectedUnit is Mech jumpMech && jumpMech.IsPsrForJumpRequired() && state._viewModel.Game is not null)
        {
            jumpActionText += state.GetPsrProbabilityText(jumpMech, PilotingSkillRollType.JumpWithDamage);
        }

        actions.Add(new StateAction(
            jumpActionText,
            true,
            () => state.HandleMovementTypeSelection(MovementType.Jump)));

        return actions;
    }

    }
}
