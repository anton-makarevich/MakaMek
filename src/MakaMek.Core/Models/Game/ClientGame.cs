using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Utils;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Core.Models.Game;

public sealed class ClientGame : BaseGame, IDisposable, IClientGame
{
    private readonly Subject<IGameCommand> _commandSubject = new();
    private readonly List<IGameCommand> _commandLog = [];
    private readonly HashSet<Guid> _playersEndedTurn = [];
    private readonly IBattleMapFactory _mapFactory;
    private readonly IHashService _hashService;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> _pendingCommands = new();
    private bool _isDisposed;
    private readonly TimeSpan _ackTimeout;
    private readonly ConcurrentDictionary<Guid, PlayerControlType> _localPlayers = new();

    public IObservable<IGameCommand> Commands => _commandSubject.AsObservable();
    public IReadOnlyList<IGameCommand> CommandLog => _commandLog;
    
    public ClientGame(IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IBattleMapFactory mapFactory,
        IHashService hashService,
        ILogger<ClientGame> logger,
        int ackTimeoutMilliseconds = 10000)
        : base(rulesProvider, mechFactory, commandPublisher, toHitCalculator, pilotingSkillCalculator, consciousnessCalculator, heatEffectsCalculator, logger)
    {
        _mapFactory = mapFactory;
        _hashService = hashService;
        _ackTimeout = TimeSpan.FromMilliseconds(ackTimeoutMilliseconds);
    }

    public IReadOnlyList<Guid> LocalPlayers => _localPlayers.Keys.ToList();

    public bool IsDisposed => _isDisposed;

    public override void HandleCommand(IGameCommand command)
    {
        if (!ShouldHandleCommand(command)) return;

        // IMPORTANT: Complete pending commands BEFORE processing command-specific logic.
        // This prevents race conditions where ActivePlayer changes trigger bot decision-making
        // before '_pendingCommands' is cleared, causing CanActivePlayerAct to incorrectly return false.
        // When we receive a re-broadcasted command, the server has already processed it successfully,
        // so completing the pending task (removing from _pendingCommands) before applying state changes
        // is semantically correct and ensures proper synchronization.
        if (command is IClientCommand { IdempotencyKey: not null } clientCommand)
        {
            // Complete the pending task with success
            CompletePendingCommand(clientCommand.IdempotencyKey.Value, true);
        }

        // Handle specific command types
        switch (command)
        {
            case SetBattleMapCommand setBattleMapCommand:
                // Create a new BattleMap from the received data
                var battleMap = _mapFactory.CreateFromData(setBattleMapCommand.MapData);
                SetBattleMap(battleMap);
                break;
            case JoinGameCommand joinGameCommand:
                OnPlayerJoined(joinGameCommand);
                break;
            case UpdatePlayerStatusCommand statusCommand:
                OnPlayerStatusUpdated(statusCommand);
                break;
            case TurnIncrementedCommand turnIncrementedCommand:
                // Use the validation method from BaseGame
                if (ValidateTurnIncrementedCommand(turnIncrementedCommand))
                {
                    foreach (var alivePlayer in AlivePlayers)
                    {
                        OnTurnEnded(alivePlayer.Id);
                    }
                    Turn = turnIncrementedCommand.TurnNumber;
                }
                break;
            case ChangePhaseCommand phaseCommand:
                TurnPhase = phaseCommand.Phase;

                // When entering the End phase, clear the players who ended turn
                // Note: ActivePlayer is set when StartPhaseCommand is received to avoid race condition
                if (phaseCommand.Phase == PhaseNames.End)
                {
                    _playersEndedTurn.Clear();
                }
                break;
            case StartPhaseCommand startPhaseCommand:
                // When the End phase is fully initialized on the server, set the first alive local player as active
                // This ensures the server has completed phase initialization before bots start making decisions
                if (startPhaseCommand.Phase == PhaseNames.End)
                {
                    var firstLocalPlayer = AlivePlayers.FirstOrDefault(p => _localPlayers.ContainsKey(p.Id));
                    PhaseStepState = firstLocalPlayer != null 
                        ? new PhaseStepState(TurnPhase, firstLocalPlayer, 0)
                        : null;
                }
                break;
            case ChangeActivePlayerCommand changeActivePlayerCommand:
                var player = Players.FirstOrDefault(p => p.Id == changeActivePlayerCommand.PlayerId);
                PhaseStepState = player != null 
                    ? new PhaseStepState(TurnPhase, player, changeActivePlayerCommand.UnitsToPlay) 
                    : null;
                break;
            case DeployUnitCommand deployUnitCommand:
                OnDeployUnit(deployUnitCommand);
                break;
            case MoveUnitCommand moveUnitCommand:
                OnMoveUnit(moveUnitCommand);
                break;
            case WeaponConfigurationCommand weaponConfigurationCommand:
                OnWeaponConfiguration(weaponConfigurationCommand);
                break;
            case WeaponAttackDeclarationCommand weaponAttackDeclarationCommand:
                OnWeaponsAttack(weaponAttackDeclarationCommand);
                break;
            case WeaponAttackResolutionCommand attackResolutionCommand:
                OnWeaponsAttackResolution(attackResolutionCommand);
                break;
            case MechFallCommand mechFallingCommand:
                OnMechFalling(mechFallingCommand);
                break;
            case MechStandUpCommand mechStandedUpCommand:
                OnMechStandUp(mechStandedUpCommand);
                break;
            case HeatUpdatedCommand heatUpdateCommand:
                OnHeatUpdate(heatUpdateCommand);
                break;
            case TurnEndedCommand turnEndedCommand:
                // Record that this player has ended their turn
                _playersEndedTurn.Add(turnEndedCommand.PlayerId);
                
                // Set the next local player who hasn't ended their turn as active
                var nextActivePlayer = AlivePlayers
                    .Where(p => !_playersEndedTurn.Contains(p.Id))
                    .FirstOrDefault(p => _localPlayers.ContainsKey(p.Id));

                PhaseStepState = nextActivePlayer != null
                    ? new PhaseStepState(TurnPhase, nextActivePlayer, 0)
                    : null;
                
                break;
            case PilotConsciousnessRollCommand consciousnessRollCommand:
                OnPilotConsciousnessRoll(consciousnessRollCommand);
                break;
            case UnitShutdownCommand shutdownCommand:
                OnUnitShutdown(shutdownCommand);
                break;
            case UnitStartupCommand restartCommand:
                OnMechRestart(restartCommand);
                break;
            case AmmoExplosionCommand explosionCommand:
                OnAmmoExplosion(explosionCommand);
                break;
            case CriticalHitsResolutionCommand criticalHitsCommand:
                OnCriticalHitsResolution(criticalHitsCommand);
                break;
            case ErrorCommand errorCommand:
                // Complete the pending task with failure
                if (errorCommand.IdempotencyKey.HasValue)
                    CompletePendingCommand(errorCommand.IdempotencyKey.Value, false);
                break;
        }

        // Log the command
        _commandLog.Add(command);

        // Publish the command to subscribers
        _commandSubject.OnNext(command);
    }

    protected override PlayerControlType? GetLocalPlayerControlType(Guid playerId)
    {
        return _localPlayers.TryGetValue(playerId, out var controlType) ? controlType : null;
    }

    public bool CanActivePlayerAct => PhaseStepState?.ActivePlayer is { } activePlayer
                                      && _localPlayers.ContainsKey(activePlayer.Id) 
                                      && activePlayer.CanAct
                                      && _pendingCommands.IsEmpty;

    /// <summary>
    /// Sends a player action command if the active player can act.
    /// Computes and assigns an idempotency key, tracks the command, and returns a task that completes when the server responds.
    /// </summary>
    /// <param name="command">Any client command to be sent</param>
    /// <typeparam name="T">Type of command that implements IClientCommand</typeparam>
    /// <returns>A task that completes with true on success, false on error</returns>
    private Task<bool> SendPlayerAction<T>(T command) where T : struct, IClientCommand
    {
        return !CanActivePlayerAct 
            ? Task.FromResult(false) 
            : SendClientCommand(command);
    }

    private async Task<bool> SendClientCommand<T>(T command) where T : struct, IClientCommand
    {
        if (!ValidateCommand(command)) return false;
        
        // Extract UnitId from the command if it has one
        var unitId = GetUnitIdFromCommand(command);

        // Compute idempotency key
        var idempotencyKey = _hashService.ComputeCommandIdempotencyKey
        (Id,
            command.PlayerId,
            typeof(T),
            Turn,
            TurnPhase.ToString(),
            unitId);

        // Check if this command is already pending
        if (_pendingCommands.TryGetValue(idempotencyKey, out var pendingCommand))
        {
            // Return the existing task
            return await pendingCommand.Task.ConfigureAwait(false);
        }

        // Assign the idempotency key to the command
        var commandWithKey = command with
        {
            GameOriginId = Id,
            IdempotencyKey = idempotencyKey
        };

        // Create a new task completion source for this command
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingCommands.TryAdd(idempotencyKey, tcs))
        {
            return await _pendingCommands[idempotencyKey].Task.ConfigureAwait(false);
        }

        // Return the task that will be completed when the server responds
        CommandPublisher.PublishCommand(commandWithKey);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(_ackTimeout)).ConfigureAwait(false);
        if (completed == tcs.Task) return await tcs.Task.ConfigureAwait(false);

        // Timeout: clean up and report failure
        tcs.TrySetResult(false);
        _pendingCommands.TryRemove(idempotencyKey, out _);
        return false;
    }

    public Task<bool> JoinGameWithUnits(IPlayer player, List<UnitData> units, List<PilotAssignmentData> pilotAssignments)
    {
        var joinCommand = new JoinGameCommand
        {
            PlayerId = player.Id,
            PlayerName = player.Name,
            GameOriginId = Id,
            Tint = player.Tint,
            Units = units,
            PilotAssignments = pilotAssignments
        };
        player.Status = PlayerStatus.Joining;
        _localPlayers.TryAdd(player.Id, player.ControlType);
        return SendClientCommand(joinCommand);
    }
    
    public Task<bool> SetPlayerReady(UpdatePlayerStatusCommand readyCommand) => SendClientCommand(readyCommand);

    public Task<bool> DeployUnit(DeployUnitCommand command) => SendPlayerAction(command);

    public Task<bool> MoveUnit(MoveUnitCommand command) => SendPlayerAction(command);

    public Task<bool> ConfigureUnitWeapons(WeaponConfigurationCommand command) => SendPlayerAction(command);

    public Task<bool> DeclareWeaponAttack(WeaponAttackDeclarationCommand command) => SendPlayerAction(command);

    public Task<bool> EndTurn(TurnEndedCommand command) => SendPlayerAction(command);

    public Task<bool> TryStandupUnit(TryStandupCommand command) => SendPlayerAction(command);

    public Task<bool> ShutdownUnit(ShutdownUnitCommand command) => SendPlayerAction(command);

    public Task<bool> StartupUnit(StartupUnitCommand command) => SendPlayerAction(command);

    public void RequestLobbyStatus(RequestGameLobbyStatusCommand statusCommand)
    {
        // Assign GameOriginId only; no idempotency/ack tracking
        // as this command is not being re-broadcasted rn
        var cmd = statusCommand with { GameOriginId = Id };
        CommandPublisher.PublishCommand(cmd);
    }

    /// <summary>
    /// Sends a PlayerLeftCommand for the specified player
    /// </summary>
    /// <param name="playerId">The ID of the player leaving</param>
    public void LeaveGame(Guid playerId)
    {
        if (!_localPlayers.ContainsKey(playerId))
        {
            return;
        }

        var playerLeftCommand = new PlayerLeftCommand
        {
            GameOriginId = Id,
            PlayerId = playerId,
            Timestamp = DateTime.UtcNow
        };
        // Call it directly as we don't want to track this command for now
        CommandPublisher.PublishCommand(playerLeftCommand);
    }

    /// <summary>
    /// Extracts the UnitId from a command if it has one.
    /// </summary>
    private static Guid? GetUnitIdFromCommand(IClientCommand command)
    {
        return command is IClientUnitCommand unitCommand ? unitCommand.UnitId : null;
    }

    /// <summary>
    /// Completes a pending command task.
    /// </summary>
    private void CompletePendingCommand(Guid idempotencyKey, bool success)
    {
        if (_pendingCommands.TryRemove(idempotencyKey, out var tcs))
        {
            tcs.TrySetResult(success);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Unsubscribe from command publisher
        CommandPublisher.Unsubscribe(HandleCommand);

        // Fail/cancel any pending waits to avoid hangs
        foreach (var kv in _pendingCommands)
            kv.Value.TrySetCanceled();
        _pendingCommands.Clear();

        // Complete and dispose subjects
        _commandSubject.OnCompleted();
        _commandSubject.Dispose();
    }
}