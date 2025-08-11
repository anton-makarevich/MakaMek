using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class HeatPhase(ServerGame game) : GamePhase(game)
{
    private int _currentPlayerIndex;
    private int _currentUnitIndex;
    
    // List of players in initiative order for heat resolution
    private List<IPlayer> _playersInOrder = [];

    public override void Enter()
    {
        base.Enter();
        
        // Initialize a heat resolution process
        _playersInOrder = Game.InitiativeOrder.ToList();
        _currentPlayerIndex = 0;
        _currentUnitIndex = 0;
        
        // Start processing heat for all units
        ProcessNextUnitHeat();
    }

    public override void HandleCommand(IGameCommand command)
    {
        // No commands to handle in this phase
    }

    public override PhaseNames Name => PhaseNames.Heat;
    
    private void ProcessNextUnitHeat()
    {
        // Check if we've processed all players
        if (_currentPlayerIndex >= _playersInOrder.Count)
        {
            Game.TransitionToNextPhase(Name);
            return;
        }

        var currentPlayer = _playersInOrder[_currentPlayerIndex];
        var units = currentPlayer.AliveUnits;

        // Check if we've processed all units for the current player
        if (_currentUnitIndex >= units.Count)
        {
            MoveToNextPlayer();
            ProcessNextUnitHeat();
            return;
        }

        var currentUnit = units[_currentUnitIndex];
        
        // Calculate and apply heat for the current unit
        CalculateAndApplyHeat(currentUnit);
        
        // Move to the next unit
        _currentUnitIndex++;
        
        // Continue processing heat for the next unit
        ProcessNextUnitHeat();
    }
    
    private void MoveToNextPlayer()
    {
        _currentPlayerIndex++;
        _currentUnitIndex = 0;
    }
    
    private void CalculateAndApplyHeat(Unit unit)
    {
        // Store previous heat before applying new heat
        var previousHeat = unit.CurrentHeat;
        
        // Get heat data from the unit
        var heatData = unit.GetHeatData(Game.RulesProvider);
        
        // Publish heat updated command
        PublishHeatUpdatedCommand(
            unit, 
            heatData,
            previousHeat);
    }
    
    private void PublishHeatUpdatedCommand(
        Unit unit, 
        HeatData heatData,
        int previousHeat)
    {
        var command = new HeatUpdatedCommand
        {
            UnitId = unit.Id,
            HeatData = heatData,
            PreviousHeat = previousHeat,
            Timestamp = DateTime.UtcNow,
            GameOriginId = Game.Id
        };
        
        Game.OnHeatUpdate(command);

        Game.CommandPublisher.PublishCommand(command);

        // Check for automatic restart if unit was shutdown due to heat
        CheckForAutomaticRestart(unit);

        // Check for heat shutdown after applying heat
        CheckForHeatShutdown(unit);

        // Process consciousness rolls for any heat damage to pilot
        ProcessConsciousnessRollsForUnit(unit);
    }

    private void CheckForAutomaticRestart(Unit unit)
    {
        if (unit is not Mech mech) return;
        if (!mech.IsShutdown) return;
        if (!mech.CurrentShutdownData.HasValue) return;

        var shutdownData = mech.CurrentShutdownData.Value;

        // Only check for automatic restart for heat shutdowns from previous turns
        if (shutdownData.Reason != ShutdownReason.Heat || shutdownData.Turn >= Game.Turn) return;

        var restartCommand = Game.HeatEffectsCalculator.AttemptRestart(mech, Game.Turn);
        if (restartCommand == null) return;

        Game.OnMechRestart(restartCommand.Value);
        Game.CommandPublisher.PublishCommand(restartCommand);
    }

    private void CheckForHeatShutdown(Unit unit)
    {
        if (unit is not Mech mech) return;

        var shutdownCommand = Game.HeatEffectsCalculator.CheckForHeatShutdown(mech, Game.Turn);
        if (shutdownCommand == null) return;

        var broadcastCommand = shutdownCommand.Value;
        broadcastCommand.GameOriginId = Game.Id;
        Game.OnUnitShutdown(broadcastCommand);
        Game.CommandPublisher.PublishCommand(broadcastCommand);
    }
}
