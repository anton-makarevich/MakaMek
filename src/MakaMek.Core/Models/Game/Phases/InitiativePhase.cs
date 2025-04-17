using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class InitiativePhase : GamePhase
{
    private readonly InitiativeOrder _initiativeOrder;

    public InitiativePhase(ServerGame game) : base(game)
    {
        _initiativeOrder = new InitiativeOrder();
    }

    public override void Enter()
    {
        _initiativeOrder.Clear();
        
        if (!Game.IsAutoRoll)
        {
            Game.SetActivePlayer(Game.AlivePlayers[0],0);
            return;
        }
        AutoRollForAllPlayers();
    }

    private void AutoRollForAllPlayers()
    {
        var playersToRoll = Game.AlivePlayers.Where(p => p.Status == PlayerStatus.Ready).ToList();
        
        while (playersToRoll.Any())
        {
            foreach (var player in playersToRoll)
            {
                Game.SetActivePlayer(player,0);
                var roll = Roll2D6();
                _initiativeOrder.AddResult(player, roll);

                Game.CommandPublisher.PublishCommand(new DiceRolledCommand
                {
                    GameOriginId = Game.Id,
                    PlayerId = player.Id,
                    Roll = roll
                });
            }

            if (!_initiativeOrder.HasTies()) break;
            // If there are ties, prepare for reroll
            playersToRoll = _initiativeOrder.GetTiedPlayers();
            _initiativeOrder.StartNewRoll(); // Start next roll number
        }

        // All rolls are complete, proceed to movement
        Game.SetInitiativeOrder(_initiativeOrder.GetOrderedPlayers());
        Game.TransitionToNextPhase(Name);
    }

    public override void HandleCommand(IGameCommand command)
    {
        if (command is not RollDiceCommand rollCommand) return;
        if (rollCommand.PlayerId != Game.ActivePlayer?.Id) return;

        var broadcastRollCommand = rollCommand;
        broadcastRollCommand.GameOriginId = Game.Id;
        Game.CommandPublisher.PublishCommand(broadcastRollCommand);

        var roll = Roll2D6();
        _initiativeOrder.AddResult(Game.ActivePlayer, roll);

        // Publish the roll result
        Game.CommandPublisher.PublishCommand(new DiceRolledCommand
        {
            GameOriginId = Game.Id,
            PlayerId = Game.ActivePlayer.Id,
            Roll = roll
        });

        // Get all players who still need to roll in this round
        var unrolledPlayers = Game.AlivePlayers
            .Where(p => p.Status == PlayerStatus.Ready)
            .Where(p => !_initiativeOrder.HasPlayerRolledInCurrentRound(p))
            .ToList();

        if (unrolledPlayers.Count != 0)
        {
            // Some players still need to roll in this round
            Game.SetActivePlayer(unrolledPlayers.First(),0);
            return;
        }

        // All players have rolled in this round
        if (_initiativeOrder.HasTies())
        {
            // If there are ties, start a new round for tied players
            var tiedPlayers = _initiativeOrder.GetTiedPlayers();
            _initiativeOrder.StartNewRoll();
            Game.SetActivePlayer(tiedPlayers.First(),0);
        }
        else
        {
            // No ties, we're done
            Game.SetInitiativeOrder(_initiativeOrder.GetOrderedPlayers());
            Game.TransitionToNextPhase(Name);
        }
    }

    private int Roll2D6()
    {
        var rolls = Game.DiceRoller.Roll2D6();
        return rolls.Sum(r => r.Result);
    }

    public override PhaseNames Name => PhaseNames.Initiative;
}
