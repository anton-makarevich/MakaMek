using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class EndPhase(ServerGame game) : GamePhase(game)
{
    private readonly HashSet<Guid> _playersEndedTurn = new();

    public override void Enter()
    {
        // Clear the set of players who have ended their turn
        _playersEndedTurn.Clear();

        // Process consciousness recovery rolls for unconscious pilots
        ProcessConsciousnessRecoveryRolls();

        // Check for victory conditions
        CheckVictoryConditions();

        // Publish StartPhaseCommand to signal that phase initialization is complete
        Game.CommandPublisher.PublishCommand(new StartPhaseCommand
        {
            GameOriginId = Game.Id,
            Phase = PhaseNames.End
        });
    }

    public override void HandleCommand(IGameCommand command)
    {
        switch (command)
        {
            case TurnEndedCommand turnEndedCommand:
                HandleTurnEndedCommand(turnEndedCommand);
                break;
            case ShutdownUnitCommand shutdownUnitCommand:
                HandleShutdownUnitCommand(shutdownUnitCommand);
                break;
            case StartupUnitCommand startupUnitCommand:
                HandleStartupUnitCommand(startupUnitCommand);
                break;
        }
    }

    private void HandleTurnEndedCommand(TurnEndedCommand turnEndedCommand)
    {

        // Verify the player is in the game
        var player = Game.Players.FirstOrDefault(p => p.Id == turnEndedCommand.PlayerId);
        if (player == null) return;

        // Record that this player has ended their turn
        _playersEndedTurn.Add(turnEndedCommand.PlayerId);

        // Broadcast the command to all clients
        var broadcastCommand = turnEndedCommand;
        broadcastCommand.GameOriginId = Game.Id;
        // Call the OnTurnEnded method on the BaseGame class
        Game.OnTurnEnded(turnEndedCommand.PlayerId);
        Game.CommandPublisher.PublishCommand(broadcastCommand);

        if (!HaveAllPlayersEndedTurn()) return;
        // All players have ended their turns, start a new turn
        Game.IncrementTurn();

        // Transition to the next phase using the phase manager
        Game.TransitionToNextPhase(Name);
    }

    private void HandleShutdownUnitCommand(ShutdownUnitCommand shutdownUnitCommand)
    {
        // Verify the player is in the game
        var player = Game.Players.FirstOrDefault(p => p.Id == shutdownUnitCommand.PlayerId);

        // Find the unit to shut down
        var unit = player?.Units.FirstOrDefault(u => u.Id == shutdownUnitCommand.UnitId);
        if (unit == null || unit.IsDestroyed || unit.IsShutdown) return;

        // Create a server shutdown command for voluntary shutdown
        var serverShutdownCommand = new UnitShutdownCommand
        {
            GameOriginId = Game.Id,
            UnitId = unit.Id,
            ShutdownData = new ShutdownData
            {
                Reason = ShutdownReason.Voluntary,
                Turn = Game.Turn
            },
            AvoidShutdownRoll = null, // No roll needed for voluntary shutdown
            IsAutomaticShutdown = false, 
            Timestamp = DateTime.UtcNow
        };

        // Apply the shutdown and broadcast to all clients
        Game.OnUnitShutdown(serverShutdownCommand);
        Game.CommandPublisher.PublishCommand(serverShutdownCommand);
    }

    private void HandleStartupUnitCommand(StartupUnitCommand startupUnitCommand)
    {
        // Verify the player is in the game
        var player = Game.Players.FirstOrDefault(p => p.Id == startupUnitCommand.PlayerId);
        if (player == null) return;

        // Find the unit to start up
        var unit = player.Units.FirstOrDefault(u => u.Id == startupUnitCommand.UnitId);
        if (unit == null || unit.IsDestroyed || !unit.IsShutdown) return;

        // Only mechs can be started up
        if (unit is not Mech mech) return;

        // Use the heat effects calculator to attempt restart
        var restartCommand = Game.HeatEffectsCalculator.AttemptRestart(mech, Game.Turn);
        if (restartCommand == null) return;

        var serverStartupCommand = restartCommand.Value;
        serverStartupCommand.GameOriginId = Game.Id;

        // Apply the startup and broadcast to all clients
        Game.OnMechRestart(serverStartupCommand);
        Game.CommandPublisher.PublishCommand(serverStartupCommand);
    }

    private bool HaveAllPlayersEndedTurn()
    {
        // Check if all players in the game have ended their turn
        return Game.AlivePlayers.All(player => _playersEndedTurn.Contains(player.Id));
    }

    /// <summary>
    /// Processes consciousness recovery rolls for unconscious pilots
    /// Recovery attempts are made for pilots who became unconscious in previous turns
    /// </summary>
    private void ProcessConsciousnessRecoveryRolls()
    {
        foreach (var player in Game.AlivePlayers)
        {
            foreach (var unit in player.AliveUnits)
            {
                if (unit.Pilot == null) continue;

                // Only attempt recovery for pilots who became unconscious in previous turns
                if (unit.Pilot.IsConscious ||
                    unit.Pilot.IsDead ||
                    unit.Pilot.UnconsciousInTurn == null ||
                    unit.Pilot.UnconsciousInTurn >= Game.Turn)
                {
                    continue;
                }

                var recoveryCommand = Game.ConsciousnessCalculator.MakeRecoveryConsciousnessRoll(unit.Pilot);
                if (recoveryCommand == null) continue;

                var broadcastCommand = recoveryCommand.Value;
                broadcastCommand.GameOriginId = Game.Id;
                Game.OnPilotConsciousnessRoll(broadcastCommand);
                Game.CommandPublisher.PublishCommand(broadcastCommand);
            }
        }
    }

    /// <summary>
    /// Checks if victory conditions are met and ends the game if so
    /// Victory conditions:
    /// - More than 1 total player in the game (to avoid single-player scenarios)
    /// - Only one (or less) player has alive units
    /// </summary>
    private void CheckVictoryConditions()
    {
        // Need more than 1 player for victory to be possible
        if (Game.Players.Count <= 1) return;

        // Count players with alive units
        var alivePlayerCount = Game.AlivePlayers.Count;

        // Victory condition: only one or zero players have alive units
        if (alivePlayerCount <= 1)
        {
            Game.StopGame(GameEndReason.Victory);
        }
    }

    public override PhaseNames Name => PhaseNames.End;
}
