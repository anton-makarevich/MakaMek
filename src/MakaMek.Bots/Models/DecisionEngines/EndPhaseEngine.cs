using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the end phase
/// </summary>
public class EndPhaseEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;

    public EndPhaseEngine(IClientGame clientGame)
    {
        _clientGame = clientGame;
    }

    public async Task MakeDecision(IPlayer player)
    {
        try
        {
            // 1. Handle shutdown units (attempt restart)
            await HandleShutdownUnits(player);

            // 2. Handle overheated units (shutdown if heat > 25)
            await HandleOverheatedUnits(player);

            // 3. End turn
            await EndTurn(player);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation
            Console.WriteLine($"EndPhaseEngine error for player {player.Name}: {ex.Message}");

            // Always try to end turn even if other actions failed
            try
            {
                await EndTurn(player);
            }
            catch
            {
                // If we can't end turn, there's nothing more we can do
            }
        }
    }

    private async Task HandleShutdownUnits(IPlayer player)
    {
        var shutdownUnits = player.AliveUnits.Where(u => u.IsShutdown).ToList();

        foreach (var unit in shutdownUnits)
        {
            // Always attempt to restart shutdown units
            var startupCommand = new StartupUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = unit.Id
            };

            await _clientGame.StartupUnit(startupCommand);
        }
    }

    private async Task HandleOverheatedUnits(IPlayer player)
    {
        var overheatedUnits = player.AliveUnits.Where(u => u.CurrentHeat > 25 && !u.IsShutdown).ToList();

        foreach (var unit in overheatedUnits)
        {
            // Shutdown overheated units to prevent damage
            var shutdownCommand = new ShutdownUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = player.Id,
                UnitId = unit.Id
            };

            await _clientGame.ShutdownUnit(shutdownCommand);
        }
    }

    private async Task EndTurn(IPlayer player)
    {
        var endTurnCommand = new TurnEndedCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = player.Id
        };

        await _clientGame.EndTurn(endTurnCommand);
    }
}

