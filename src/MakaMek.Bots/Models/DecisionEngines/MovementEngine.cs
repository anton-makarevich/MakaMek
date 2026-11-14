using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Exceptions;
using Sanet.MakaMek.Bots.Models.Logger;
using Sanet.MakaMek.Bots.Models.Movement;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Decision engine for the movement phase
/// </summary>
public class MovementEngine : IBotDecisionEngine
{
    private readonly IClientGame _clientGame;
    private readonly IUnitPriorityCalculator _priorityCalculator;
    private readonly IMovementPathPlanner _pathPlanner;
    private readonly IMovementCommandSender _commands;

    /// <summary>
    /// Creates the engine with the default movement evaluation graph. Prefer the constructor that
    /// takes explicit <see cref="MovementEvaluationComponents"/> when composing the bot.
    /// </summary>
    public MovementEngine(IClientGame clientGame, ITacticalEvaluator evaluator, Random? random = null)
        : this(clientGame, MovementEvaluationComponents.CreateDefault(clientGame, evaluator, random))
    {
    }

    public MovementEngine(IClientGame clientGame, MovementEvaluationComponents components)
    {
        _clientGame = clientGame;
        _priorityCalculator = components.PriorityCalculator;
        _pathPlanner = components.PathPlanner;
        _commands = components.CommandSender;
    }

    public async Task MakeDecision(IPlayer player, ITurnState? turnState = null, BotSettings settings = default)
    {
        IUnit? unitToMove = null;
        try
        {
            // 1. Get all friendly units that haven't moved
            var myUnitsToMove = turnState?.PhaseActiveUnitId != null
                ? player.AliveUnits
                    .Where(u => u.Id == turnState.PhaseActiveUnitId)
                    .ToList()
                : player.AliveUnits
                    .Where(u => u is { HasMoved: false })
                    .ToList();

            if (myUnitsToMove.Count == 0)
            {
                // No units to move
                _clientGame.Logger.LogNoUnitsToMove(player.Name);
                return;
            }

            var immobileUnit = myUnitsToMove.FirstOrDefault(u => u.IsImmobile);
            if (immobileUnit != null)
            {
                await _commands.SkipTurn(player, immobileUnit);
                return;
            }

            // 2. Calculate enemy units and friendly positions once (static during this decision)
            var enemyUnits = _clientGame.Players
                .Where(p => p.Id != player.Id)
                .SelectMany(p => p.AliveUnits)
                .Where(u => !u.Status.HasFlag(UnitStatus.Destroyed))
                .ToList();

            var friendlyPositions = player.AliveUnits
                .Where(u => u is { IsDeployed: true, Position: not null })
                .Select(u => u.Position!.Coordinates)
                .ToHashSet();

            // 3. Analyze Phase State
            var enemyUnitsToMoveCount = enemyUnits.Count(u => !u.HasMoved);

            var state = new MovementPhaseState(
                EnemyUnitsRemaining: enemyUnitsToMoveCount
            );

            // 4. Calculate Priorities
            var scoredUnits = myUnitsToMove.Select(u => new
                {
                    Unit = u,
                    Priority = _priorityCalculator.CalculatePriority(u, state)
                })
                .OrderByDescending(u => u.Priority)
                .ThenByDescending(u => u.Unit.Id) // Deterministic tie-breaker
                .ToList();

            var bestCandidate = scoredUnits.First();
            unitToMove = bestCandidate.Unit;

            _clientGame.Logger.LogSelectedUnitWithRoleAndPriority(
                unitToMove.Name,
                unitToMove.GetTacticalRole(),
                bestCandidate.Priority);

            // 5. Execute Move for a selected unit
            await ExecuteMoveForUnit(player, unitToMove, enemyUnits, friendlyPositions, turnState, settings);
        }
        catch (BotDecisionException ex)
        {
            // Rethrow BotDecisionException to let caller handle decision failures
            _clientGame.Logger.LogError(ex, "MovementEngine error for player {PlayerName}: {Message}", player.Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - graceful degradation for unexpected errors
            _clientGame.Logger.LogError(ex, "MovementEngine error for player {PlayerName}: {Message}", player.Name, ex.Message);
            await _commands.SkipTurn(player, unitToMove);
        }
    }

    /// <summary>
    /// Executes movement for a unit using intelligent position evaluation
    /// </summary>
    private async Task ExecuteMoveForUnit(
        IPlayer player,
        IUnit unit,
        IReadOnlyList<IUnit> enemyUnits,
        IReadOnlySet<HexCoordinates> friendlyPositions,
        ITurnState? turnState,
        BotSettings settings)
    {
        // Handle prone mechs - try to stand up
        if (await _commands.TryStandUp(player, unit))
            return;

        if (_clientGame.BattleMap == null || unit.Position == null)
        {
            await _commands.SkipTurn(player, unit);
            return;
        }

        var bestPath = _pathPlanner.SelectBestPath(unit, enemyUnits, friendlyPositions, turnState, settings);
        if (bestPath == null)
        {
            // If no valid paths, stand still
            await _commands.SkipTurn(player, unit);
            return;
        }

        // Execute the move using the stored path (no need to recalculate)
        await _commands.Move(player, unit, bestPath);
    }
}
