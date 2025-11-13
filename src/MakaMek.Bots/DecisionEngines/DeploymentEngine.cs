using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Decision engine for the deployment phase
/// </summary>
public class DeploymentEngine : IBotDecisionEngine
{
    private readonly ClientGame _clientGame;
    private readonly IPlayer _player;
    private readonly BotDifficulty _difficulty;
    private readonly Random _random = new();

    public DeploymentEngine(ClientGame clientGame, IPlayer player, BotDifficulty difficulty)
    {
        _clientGame = clientGame;
        _player = player;
        _difficulty = difficulty;
    }

    public async Task MakeDecision()
    {
        try
        {
            // Find undeployed units
            var undeployedUnit = _player.Units.FirstOrDefault(u => !u.IsDeployed);
            if (undeployedUnit == null)
            {
                // No units to deploy, skip turn
                await SkipTurn();
                return;
            }

            // Get valid deployment hexes (all unoccupied hexes on the map)
            var validHexes = GetValidDeploymentHexes();
            if (validHexes.Count == 0)
            {
                // No valid hexes, skip turn
                await SkipTurn();
                return;
            }

            // Select random hex and direction
            var selectedHex = validHexes[_random.Next(validHexes.Count)];
            var selectedDirection = GetRandomDirection();

            // Create and publish DeployUnitCommand
            var command = new DeployUnitCommand
            {
                GameOriginId = _clientGame.Id,
                PlayerId = _player.Id,
                UnitId = undeployedUnit.Id,
                Position = selectedHex.ToData(),
                Direction = (int)selectedDirection
            };

            await _clientGame.DeployUnit(command);
        }
        catch
        {
            // If anything fails, skip turn to avoid blocking the game
            await SkipTurn();
        }
    }

    private List<HexCoordinates> GetValidDeploymentHexes()
    {
        if (_clientGame.BattleMap == null)
            return [];

        // Get all hexes on the map
        var allHexes = _clientGame.BattleMap.GetHexes().Select(h => h.Coordinates).ToList();

        // Get occupied hexes
        var occupiedHexes = _clientGame.Players
            .SelectMany(p => p.Units)
            .Where(u => u.IsDeployed && u.Position != null)
            .Select(u => u.Position!.Coordinates)
            .ToHashSet();

        // Return unoccupied hexes
        return allHexes.Where(h => !occupiedHexes.Contains(h)).ToList();
    }

    private HexDirection GetRandomDirection()
    {
        var directions = new[]
        {
            HexDirection.Top,
            HexDirection.TopRight,
            HexDirection.BottomRight,
            HexDirection.Bottom,
            HexDirection.BottomLeft,
            HexDirection.TopLeft
        };

        return directions[_random.Next(directions.Length)];
    }

    private async Task SkipTurn()
    {
        // In deployment phase, we don't have a TurnEndedCommand
        // The phase will automatically progress when all units are deployed
        // So we just return without doing anything
        await Task.CompletedTask;
    }
}

