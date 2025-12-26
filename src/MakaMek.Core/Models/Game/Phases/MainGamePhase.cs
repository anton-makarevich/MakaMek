using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public abstract class MainGamePhase : GamePhase
{
    private readonly TurnOrder _turnOrder;
    private int _remainingUnits;

    protected MainGamePhase(ServerGame game) : base(game)
    {
        _turnOrder = new TurnOrder();
    }

    public override void Enter()
    {
        _turnOrder.CalculateOrder(Game.InitiativeOrder);
        SetNextPlayerActive();
    }

    private void SetNextPlayerActive()
    {
        var nextStep = _turnOrder.GetNextStep();
        if (nextStep == null)
        {
            Game.TransitionToNextPhase(Name);
            return;
        }

        _remainingUnits = nextStep.UnitsToMove;
        Game.SetActivePlayer(nextStep.Player, _remainingUnits);
    }

    protected void HandleUnitAction(IGameCommand command, Guid playerId)
    {
        if (playerId != Game.PhaseStepState?.ActivePlayer.Id) return;

        ProcessCommand(command);
        
        _remainingUnits--;
        if (_remainingUnits <= 0)
        {
            SetNextPlayerActive();
            return;
        }
        Game.SetActivePlayer(Game.PhaseStepState?.ActivePlayer, _remainingUnits);
    }

    protected abstract void ProcessCommand(IGameCommand command);
}
