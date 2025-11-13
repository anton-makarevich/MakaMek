using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the end phase
/// </summary>
public class EndPhaseEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;
    private const int VoluntaryShutdownHeatThreshold = 26; // Shutdown if heat >= 26

    public EndPhaseEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public async Task MakeDecision()
    {
        try
        {
            // Process shutdown units - always attempt restart
            await ProcessShutdownUnits();

            // Process overheated units - shutdown if heat >= 26
            await ProcessOverheatedUnits();

            // End turn
            await EndTurn();
        }
        catch
        {
            // If anything fails, still try to end turn to avoid blocking the game
            try
            {
                await EndTurn();
            }
            catch
            {
                // Ignore errors when ending turn
            }
        }
    }

    private async Task ProcessShutdownUnits()
    {
        // Find all shutdown units
        var shutdownUnits = _player.AliveUnits
            .Where(u => u.IsShutdown)
            .ToList();

        foreach (var unit in shutdownUnits)
        {
            // Attempt to restart the unit
            var command = new StartupUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id
            };

            await _clientGame.StartupUnit(command);
        }
    }

    private async Task ProcessOverheatedUnits()
    {
        // Find units that are overheated and not already shutdown
        var overheatedUnits = _player.AliveUnits
            .Where(u => !u.IsShutdown && u.CurrentHeat >= VoluntaryShutdownHeatThreshold)
            .ToList();

        foreach (var unit in overheatedUnits)
        {
            // Voluntarily shutdown the unit to avoid damage
            var command = new ShutdownUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id
            };

            await _clientGame.ShutdownUnit(command);
        }
    }

    private async Task EndTurn()
    {
        var command = new TurnEndedCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id
        };

        await _clientGame.EndTurn(command);
    }
}

