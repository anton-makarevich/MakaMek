using System.Collections.Concurrent;
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
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Utils;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

namespace Sanet.MakaMek.Core.Models.Game;

public sealed class ClientGame : BaseGame, IDisposable, IClientGame
{
    private readonly HashSet<Guid> _playersEndedTurn = [];
    private readonly IBattleMapFactory _mapFactory;
    private readonly IHashService _hashService;
    private readonly Guid? _serverGameId;
    private record PendingCommand(TaskCompletionSource<bool> Tcs, string CommandType);

    private readonly ConcurrentDictionary<Guid, PendingCommand> _pendingCommands = new();
    private readonly Lock _pendingCommandsSync = new();
    private bool _isDisposed;
    private readonly TimeSpan _ackTimeout;
    private readonly ConcurrentDictionary<Guid, PlayerControlType> _localPlayers = new();
    private readonly ClientCommandApplier _commandApplier;

    public ClientGame(IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IPhysicalAttackEligibility physicalAttackEligibility,
        IPhysicalAttackCalculator physicalAttackCalculator,
        IMovementAttackDeclarationValidator movementAttackDeclarationValidator,
        IBattleMapFactory mapFactory,
        IHashService hashService,
        ILogger<ClientGame> logger,
        Guid? serverGameId = null,
        int ackTimeoutMilliseconds = 10000)
        : base(rulesProvider, mechFactory, commandPublisher, toHitCalculator, pilotingSkillCalculator, consciousnessCalculator, heatEffectsCalculator, physicalAttackEligibility, physicalAttackCalculator, movementAttackDeclarationValidator, logger)
    {
        _mapFactory = mapFactory;
        _hashService = hashService;
        _serverGameId = serverGameId;
        _ackTimeout = TimeSpan.FromMilliseconds(ackTimeoutMilliseconds);
        _commandApplier = new ClientCommandApplier(this);
    }

    internal IBattleMapFactory MapFactory => _mapFactory;
    internal HashSet<Guid> PlayersEndedTurn => _playersEndedTurn;
    internal bool IsLocalPlayer(Guid playerId) => _localPlayers.ContainsKey(playerId);

    public IReadOnlyList<Guid> LocalPlayers => _localPlayers.Keys.ToList();

    /// <inheritdoc />
    public Guid? ServerGameId => _serverGameId;

    public bool IsDisposed => _isDisposed;

    /// <inheritdoc />
    public bool HasPendingCommands => !_pendingCommands.IsEmpty;

    /// <inheritdoc />
    public event Action? PendingCommandsChanged;

    /// <inheritdoc />
    public event Action? CommandTimedOut;

    /// <inheritdoc />
    public event Action<ErrorCode>? CommandRejectedLocally;

    protected internal override bool ShouldHandleCommand(IGameCommand command)
    {
        if (!base.ShouldHandleCommand(command)) return false;

        // When connected to a server, only process commands that the server validated and rebroadcast.
        // This prevents raw client commands (from other clients) from being double-applied:
        // once via the transport fan-out and once via the server rebroadcast.
        return !_serverGameId.HasValue || command.GameOriginId == _serverGameId.Value;
    }

    public override void HandleCommand(IGameCommand command)
    {
        if (!ShouldHandleCommand(command)) return;

        _commandApplier.Apply(command);

        RecordCommand(command);
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
        var validationResult = ValidateCommand(command);
        if (!validationResult.IsValid)
        {
            Logger.LogWarning(
                "Command {CommandType} rejected by validation: {ErrorCode}. PlayerId: {PlayerId}, GameOriginId: {GameOriginId}",
                typeof(T).Name,
                validationResult.ErrorCode,
                command.PlayerId,
                command.GameOriginId);
            CommandRejectedLocally?.Invoke(validationResult.ErrorCode ?? ErrorCode.UnknownError);
            return false;
        }
        
        // Extract UnitId from the command if it has one
        var unitId = GetUnitIdFromCommand(command);
        var attempt = GetCommandAttempt(command);

        // Compute idempotency key
        var idempotencyKey = _hashService.ComputeCommandIdempotencyKey
        (Id,
            command.PlayerId,
            typeof(T),
            Turn,
            TurnPhase.ToString(),
            unitId,
            attempt,
            command.GetPayloadHash());

        // Assign the idempotency key to the command
        var commandWithKey = command with
        {
            GameOriginId = Id,
            IdempotencyKey = idempotencyKey
        };

        // Create a new task completion source for this command
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingCommand = new PendingCommand(tcs, typeof(T).Name);

        // Register under the same lock Dispose uses for cleanup so disposal and
        // registration are serialized: once disposal begins no new command can be
        // added, and a command that registers first is always cancelled by Dispose.
        var registered = false;
        Task<bool>? existingTask = null;
        lock (_pendingCommandsSync)
        {
            if (_isDisposed) return false;

            if (_pendingCommands.TryAdd(idempotencyKey, pendingCommand))
            {
                registered = true;
            }
            else if (_pendingCommands.TryGetValue(idempotencyKey, out var existingPending))
            {
                existingTask = existingPending.Tcs.Task;
            }
            // Key was removed between TryAdd failure and lookup; treat as not pending.
        }

        if (existingTask != null) return await existingTask.ConfigureAwait(false);
        if (!registered) return false;

        PendingCommandsChanged?.Invoke();

        // Return the task that will be completed when the server responds
        CommandPublisher.PublishCommand(commandWithKey);

        using var timeoutCts = new CancellationTokenSource(_ackTimeout);
        try
        {
            return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Timeout: clean up and report failure
            tcs.TrySetResult(false);
            CommandTimedOut?.Invoke();
            if (_pendingCommands.TryRemove(idempotencyKey, out _))
                PendingCommandsChanged?.Invoke();
            return false;
        }
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

    /// <summary>
    /// Declares a physical attack for the active unit, or no attack when the declaration is null.
    /// </summary>
    public Task<bool> DeclarePhysicalAttack(PhysicalAttackCommand command) => SendPlayerAction(command);

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
    
    private int GetCommandAttempt(IClientCommand command)
    {
        if (command is not TryStandupCommand standupCommand) return 0;
        var unit = Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == standupCommand.UnitId);
        if (unit is Mech mech)
            return mech.StandupAttempts;
        return 0;
    }

    /// <summary>
    /// Completes a pending command task.
    /// </summary>
    internal void CompletePendingCommand(Guid idempotencyKey, bool success)
    {
        if (_pendingCommands.TryRemove(idempotencyKey, out var pendingCommand))
        {
            pendingCommand.Tcs.TrySetResult(success);
            PendingCommandsChanged?.Invoke();
        }

        if (success) return;
        
        Logger.LogWarning("Command {CommandType} (key: {IdempotencyKey}) was rejected",
            pendingCommand?.CommandType ?? "Unknown", idempotencyKey);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Unsubscribe from command publisher
        CommandPublisher.Unsubscribe(HandleCommand);

        // Fail/cancel any pending waits to avoid hangs. Registration in
        // SendClientCommand is synchronized with this cleanup, so no command can be
        // added after disposal begins. Each entry is removed before it is cancelled,
        // so a command registered concurrently with disposal is either cancelled here
        // or left intact - clearing the whole dictionary afterwards would drop such an
        // entry without ever completing its task, leaving the caller waiting for the timeout.
        var cancelledAny = false;
        lock (_pendingCommandsSync)
        {
            foreach (var key in _pendingCommands.Keys)
            {
                if (!_pendingCommands.TryRemove(key, out var pendingCommand)) continue;
                pendingCommand.Tcs.TrySetCanceled();
                cancelledAny = true;
            }
        }

        // Report what was actually removed rather than a count sampled before the loop, which a
        // concurrent completion could already have made wrong.
        if (cancelledAny)
            PendingCommandsChanged?.Invoke();

        DisposeCommandResources();
    }
}


