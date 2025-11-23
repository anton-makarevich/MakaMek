using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the end phase
/// </summary>
public class EndPhaseEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;

    public EndPhaseEngine(IClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public async Task MakeDecision()
    {
        try
        {
            // 1. Handle shutdown units (attempt restart)
            await HandleShutdownUnits();

            // 2. Handle overheated units (shutdown if heat > 25)
            await HandleOverheatedUnits();

            // 3. End turn
            await EndTurn();
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation
            Console.WriteLine($"EndPhaseEngine error for player {_player.Name}: {ex.Message}");

            // Always try to end turn even if other actions failed
            try
            {
                await EndTurn();
            }
            catch
            {
                // If we can't end turn, there's nothing more we can do
            }
        }
    }

    private async Task HandleShutdownUnits()
    {
        var shutdownUnits = _player.AliveUnits.Where(u => u.IsShutdown).ToList();

        foreach (var unit in shutdownUnits)
        {
            // Always attempt to restart shutdown units
            var startupCommand = new StartupUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id
            };

            await _clientGame.StartupUnit(startupCommand);
        }
    }

    private async Task HandleOverheatedUnits()
    {
        var overheatedUnits = _player.AliveUnits.Where(u => u.CurrentHeat > 25 && !u.IsShutdown).ToList();

        foreach (var unit in overheatedUnits)
        {
            // Shutdown overheated units to prevent damage
            var shutdownCommand = new ShutdownUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = _player.Id,
                UnitId = unit.Id
            };

            await _clientGame.ShutdownUnit(shutdownCommand);
        }
    }

    private async Task EndTurn()
    {
        var endTurnCommand = new TurnEndedCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _player.Id
        };

        await _clientGame.EndTurn(endTurnCommand);
    }
}

