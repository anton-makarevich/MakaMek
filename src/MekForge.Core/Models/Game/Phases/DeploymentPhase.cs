using Sanet.MekForge.Core.Models.Game.Commands;
using Sanet.MekForge.Core.Models.Game.Commands.Client;
using Sanet.MekForge.Core.Models.Game.Players;

namespace Sanet.MekForge.Core.Models.Game.Phases;

public class DeploymentPhase(ServerGame game) : GamePhase(game)
{
    private Queue<IPlayer> _deploymentOrderQueue = new();

    public override void Enter()
    {
        RandomizeDeploymentOrder();
        SetNextDeployingPlayer();
    }

    public override void HandleCommand(IGameCommand command)
    {
        if (command is not DeployUnitCommand deployCommand) return;
        if (deployCommand.PlayerId != Game.ActivePlayer?.Id) return;

        var broadcastCommand = deployCommand;
        broadcastCommand.GameOriginId = Game.Id;
        Game.OnDeployUnit(deployCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);
        HandleDeploymentProgress();
    }

    private void HandleDeploymentProgress()
    {
        if (Game.ActivePlayer != null && !Game.ActivePlayer.Units.All(unit => unit.IsDeployed))
        {
            Game.SetActivePlayer(Game.ActivePlayer, Game.ActivePlayer.Units.Count(u => !u.IsDeployed));
            return;
        }

        if (_deploymentOrderQueue.Count > 0)
        {
            SetNextDeployingPlayer();
        }
        else if (AllUnitsDeployed())
        {
            Game.TransitionToPhase(new InitiativePhase(Game));
        }
    }

    private bool AllUnitsDeployed()
    {
        return Game.Players
            .Where(p => p.Status == PlayerStatus.Playing)
            .All(p => p.Units.All(u => u.IsDeployed));
    }

    private void RandomizeDeploymentOrder()
    {
        var players = Game.Players.Where(p => p.Status == PlayerStatus.Playing).ToList();
        var randomizedPlayers = players.OrderBy(_ => Guid.NewGuid()).ToList();
        _deploymentOrderQueue = new Queue<IPlayer>(randomizedPlayers);
    }

    private void SetNextDeployingPlayer()
    {
        var nextPlayer = _deploymentOrderQueue.Dequeue();
        Game.SetActivePlayer(nextPlayer, nextPlayer.Units.Count(u=>!u.IsDeployed));
    }

    public override PhaseNames Name => PhaseNames.Deployment;
}
