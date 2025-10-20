using System.Reactive.Subjects;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
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
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Models.Game;

public sealed class ClientGame : BaseGame, IDisposable
{
    private readonly Subject<IGameCommand> _commandSubject = new();
    private readonly List<IGameCommand> _commandLog = [];
    private readonly HashSet<Guid> _playersEndedTurn = [];
    private readonly IBattleMapFactory _mapFactory;
    private readonly Dictionary<Guid, TaskCompletionSource<bool>> _pendingCommands = new();
    private bool _isDisposed;

    public IObservable<IGameCommand> Commands => _commandSubject.AsObservable();
    public IReadOnlyList<IGameCommand> CommandLog => _commandLog;
    
    public ClientGame(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IBattleMapFactory mapFactory)
        : base(rulesProvider, mechFactory, commandPublisher, toHitCalculator, pilotingSkillCalculator, consciousnessCalculator, heatEffectsCalculator)
    {
        _mapFactory = mapFactory;
    }

    public List<Guid> LocalPlayers { get; } = [];
    
    public override bool IsDisposed => _isDisposed;

    public override void HandleCommand(IGameCommand command)
    {
        if (!ShouldHandleCommand(command)) return;
        
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
                var localPlayer = Players.FirstOrDefault(p => p.Id == joinGameCommand.PlayerId);
                if (localPlayer != null)
                {
                    localPlayer.Status = PlayerStatus.Joined;
                }
                break;
            case UpdatePlayerStatusCommand statusCommand:
                OnPlayerStatusUpdated(statusCommand);
                break;
            case TurnIncrementedCommand turnIncrementedCommand:
                // Use the validation method from BaseGame
                if (ValidateTurnIncrementedCommand(turnIncrementedCommand))
                {
                    Turn = turnIncrementedCommand.TurnNumber;
                }
                break;
            case ChangePhaseCommand phaseCommand:
                TurnPhase = phaseCommand.Phase;
                
                // When entering End phase, clear the players who ended turn and set first local player as active
                if (phaseCommand.Phase == PhaseNames.End)
                {
                    _playersEndedTurn.Clear();
                     ActivePlayer = AlivePlayers.FirstOrDefault(p =>p.Id == LocalPlayers.FirstOrDefault());
                }
                break;
            case ChangeActivePlayerCommand changeActivePlayerCommand:
                var player = Players.FirstOrDefault(p => p.Id == changeActivePlayerCommand.PlayerId);
                ActivePlayer = player;
                UnitsToPlayCurrentStep = changeActivePlayerCommand.UnitsToPlay;
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
                OnTurnEnded(turnEndedCommand);
                // Record that this player has ended their turn
                _playersEndedTurn.Add(turnEndedCommand.PlayerId);

                // If we're in the End phase and the player who just ended their turn was the active player
                if (TurnPhase == PhaseNames.End &&
                    ActivePlayer != null &&
                    turnEndedCommand.PlayerId == ActivePlayer.Id)
                {
                    // Set the next local player who hasn't ended their turn as active
                    ActivePlayer = Players
                        .Where(p => _playersEndedTurn.Contains(p.Id) == false)
                        .FirstOrDefault(p => LocalPlayers.Any(lp => lp == p.Id));
                }
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

        // Check if this is a re-broadcasted client command with IdempotencyKey
        if (command is IClientCommand { IdempotencyKey: not null } clientCommand)
        {
            // Complete the pending task with success
            CompletePendingCommand(clientCommand.IdempotencyKey.Value, true);
        }

        // Log the command
        _commandLog.Add(command);

        // Publish the command to subscribers
        _commandSubject.OnNext(command);
    }

    public bool CanActivePlayerAct => ActivePlayer != null 
                                      && LocalPlayers.Contains(ActivePlayer.Id) 
                                      && ActivePlayer.CanAct;

    public void JoinGameWithUnits(IPlayer player, List<UnitData> units, List<PilotAssignmentData> pilotAssignments)
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
        LocalPlayers.Add(player.Id);
        if (ValidateCommand(joinCommand))
        {
            CommandPublisher.PublishCommand(joinCommand);
        }
    }
    
    public void SetPlayerReady(UpdatePlayerStatusCommand readyCommand)
    {
        if (ValidateCommand(readyCommand))
        {
            readyCommand.GameOriginId = Id;
            CommandPublisher.PublishCommand(readyCommand);
        }
    }

    /// <summary>
    /// Sends a player action command if the active player can act.
    /// Computes and assigns an idempotency key, tracks the command, and returns a task that completes when the server responds.
    /// </summary>
    /// <param name="command">Any client command to be sent</param>
    /// <typeparam name="T">Type of command that implements IClientCommand</typeparam>
    /// <returns>A task that completes with true on success, false on error</returns>
    private async Task<bool> SendPlayerAction<T>(T command) where T : struct, IClientCommand
    {
        if (!CanActivePlayerAct) return false;

        // Extract UnitId from the command if it has one
        var unitId = GetUnitIdFromCommand(command);

        // Compute idempotency key
        var idempotencyKey = ComputeIdempotencyKey(command.PlayerId, typeof(T), unitId);

        // Check if this command is already pending
        if (_pendingCommands.TryGetValue(idempotencyKey, out var pendingCommand))
        {
            // Return the existing task
            return await pendingCommand.Task;
        }

        // Create a new task completion source for this command
        var tcs = new TaskCompletionSource<bool>();
        _pendingCommands[idempotencyKey] = tcs;

        // Assign the idempotency key to the command
        var commandWithKey = command with { IdempotencyKey = idempotencyKey };

        // Publish the command
        CommandPublisher.PublishCommand(commandWithKey);

        // Return the task that will be completed when the server responds
        return await tcs.Task;
    }

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
        CommandPublisher.PublishCommand(statusCommand);
    }

    /// <summary>
    /// Sends a PlayerLeftCommand for the specified player
    /// </summary>
    /// <param name="playerId">The ID of the player leaving</param>
    public void LeaveGame(Guid playerId)
    {
        if (!LocalPlayers.Contains(playerId))
        {
            return;
        }

        var playerLeftCommand = new PlayerLeftCommand
        {
            GameOriginId = Id,
            PlayerId = playerId,
            Timestamp = DateTime.UtcNow
        };
        CommandPublisher.PublishCommand(playerLeftCommand);
    }

    /// <summary>
    /// Computes a deterministic idempotency key for a command.
    /// The key is based on GameId, PlayerId, UnitId (optional), Phase, Turn, and CommandType.
    /// </summary>
    /// <param name="playerId">The ID of the player sending the command</param>
    /// <param name="commandType">The type of the command</param>
    /// <param name="unitId">Optional unit ID for unit-specific commands</param>
    /// <returns>A deterministic GUID that serves as the idempotency key</returns>
    public Guid ComputeIdempotencyKey(Guid playerId, Type commandType, Guid? unitId = null)
    {
        // Build the input string for hashing
        var input = $"{Id}:{playerId}:{unitId?.ToString() ?? "null"}:{TurnPhase}:{Turn}:{commandType.Name}";

        // Compute SHA256 hash
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        // Take first 16 bytes to create a GUID
        return new Guid(hash[..16]);
    }

    /// <summary>
    /// Extracts the UnitId from a command if it has one.
    /// </summary>
    private static Guid? GetUnitIdFromCommand(IClientCommand command)
    {
        return command switch
        {
            DeployUnitCommand deployCommand => deployCommand.UnitId,
            MoveUnitCommand moveCommand => moveCommand.UnitId,
            WeaponConfigurationCommand configCommand => configCommand.UnitId,
            WeaponAttackDeclarationCommand attackCommand => attackCommand.AttackerId,
            TryStandupCommand standupCommand => standupCommand.UnitId,
            ShutdownUnitCommand shutdownCommand => shutdownCommand.UnitId,
            StartupUnitCommand startupCommand => startupCommand.UnitId,
            PhysicalAttackCommand physicalCommand => physicalCommand.AttackerUnitId,
            _ => null
        };
    }

    /// <summary>
    /// Completes a pending command task.
    /// </summary>
    private void CompletePendingCommand(Guid idempotencyKey, bool success)
    {
        if (_pendingCommands.Remove(idempotencyKey, out var tcs))
        {
            tcs.SetResult(success);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        // Unsubscribe from command publisher
        CommandPublisher.Unsubscribe(HandleCommand);

        // Complete and dispose subjects
        _commandSubject.OnCompleted();
        _commandSubject.Dispose();
    }
}