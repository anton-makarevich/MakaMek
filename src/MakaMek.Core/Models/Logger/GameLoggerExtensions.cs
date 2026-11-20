using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Core.Models.Logger;

public static partial class GameLoggerExtensions
{
    [LoggerMessage(LogLevel.Information, "Player {PlayerName} already joined the game.", SkipEnabledCheck = true)]
    public static partial void LogPlayerAlreadyJoinedGame(this ILogger logger, string playerName);

    [LoggerMessage(LogLevel.Information, "Hex {Position} is already occupied.", SkipEnabledCheck = true)]
    public static partial void LogHexAlreadyOccupied(this ILogger logger, HexCoordinates position);

    [LoggerMessage(LogLevel.Warning, "Command {CommandType} rejected by validation: {ErrorCode}. PlayerId: {PlayerId}, GameOriginId: {GameOriginId}", SkipEnabledCheck = true)]
    public static partial void LogCommandRejectedByValidation(this ILogger logger, string commandType, ErrorCode? errorCode, Guid playerId, Guid gameOriginId);

    [LoggerMessage(LogLevel.Warning, "Command {CommandType} (key: {IdempotencyKey}) was rejected", SkipEnabledCheck = true)]
    public static partial void LogCommandRejected(this ILogger logger, string commandType, Guid idempotencyKey);

    [LoggerMessage(LogLevel.Information, "Connected to LAN server at {ServerAddress}", SkipEnabledCheck = true)]
    public static partial void LogConnectedToLanServer(this ILogger logger, string serverAddress);

    [LoggerMessage(LogLevel.Error, "Failed to connect to LAN server at {ServerAddress}", SkipEnabledCheck = true)]
    public static partial void LogFailedToConnectToLanServer(this ILogger logger, string serverAddress, Exception exception);

    [LoggerMessage(LogLevel.Information, "Joined relay room {RoomCode} for game {GameId} {GameVersion}, connected to host game {HostGameId}", SkipEnabledCheck = true)]
    public static partial void LogJoinedRelayRoom(this ILogger logger, string roomCode, string gameId, string? gameVersion, Guid hostGameId);

    [LoggerMessage(LogLevel.Warning, "Failed to remove device session from relay room {RoomCode}", SkipEnabledCheck = true)]
    public static partial void LogFailedToRemoveDeviceSessionFromRelayRoom(this ILogger logger, string roomCode, Exception exception);

    [LoggerMessage(LogLevel.Error, "Error during GameConnector teardown", SkipEnabledCheck = true)]
    public static partial void LogErrorDuringGameConnectorTeardown(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Deferred local lobby initialization: relay room {RoomCode} could not be locked", SkipEnabledCheck = true)]
    public static partial void LogDeferredLocalLobbyInitialization(this ILogger logger, string? roomCode);

    [LoggerMessage(LogLevel.Debug, "Failed to lock online room during StopHosting", SkipEnabledCheck = true)]
    public static partial void LogFailedToLockOnlineRoomDuringStopHosting(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Deferred hosting shutdown: relay room {RoomCode} could not be locked", SkipEnabledCheck = true)]
    public static partial void LogDeferredHostingShutdown(this ILogger logger, string? roomCode);

    [LoggerMessage(LogLevel.Debug, "Failed to remove online publisher during StopHosting", SkipEnabledCheck = true)]
    public static partial void LogFailedToRemoveOnlinePublisherDuringStopHosting(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Failed to stop the network host service", SkipEnabledCheck = true)]
    public static partial void LogFailedToStopNetworkHostService(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Debug, "Failed to remove LAN publisher from the transport adapter", SkipEnabledCheck = true)]
    public static partial void LogFailedToRemoveLanPublisher(this ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Lock relay room {RoomCode} failed: {ErrorCode} {ErrorMessage}", SkipEnabledCheck = true)]
    public static partial void LogLockRelayRoomFailed(this ILogger logger, string? roomCode, RelayClientErrorCode? errorCode, string? errorMessage);

    [LoggerMessage(LogLevel.Warning, "Lock relay room {RoomCode} was cancelled", SkipEnabledCheck = true)]
    public static partial void LogLockRelayRoomCancelled(this ILogger logger, string? roomCode);

    [LoggerMessage(LogLevel.Warning, "Failed to lock relay room {RoomCode}", SkipEnabledCheck = true)]
    public static partial void LogFailedToLockRelayRoom(this ILogger logger, string? roomCode, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Failed to create relay room for game {GameId}: {ErrorCode} {ErrorMessage}", SkipEnabledCheck = true)]
    public static partial void LogFailedToCreateRelayRoom(this ILogger logger, Guid gameId, RelayClientErrorCode? errorCode, string? errorMessage);

    [LoggerMessage(LogLevel.Information, "Hosted relay room {RoomCode} for game {GameId} {GameVersion}; relay publisher connected", SkipEnabledCheck = true)]
    public static partial void LogHostedRelayRoom(this ILogger logger, string? roomCode, Guid gameId, string gameVersion);

    [LoggerMessage(LogLevel.Warning, "Failed to lock relay room {RoomCode} during failure cleanup", SkipEnabledCheck = true)]
    public static partial void LogFailedToLockRelayRoomDuringFailureCleanup(this ILogger logger, string? roomCode, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Refusing relay room {RoomCode}: it reported game {GameId} hosted by {HostGameId}", SkipEnabledCheck = true)]
    public static partial void LogRefusingRelayRoom(this ILogger logger, string roomCode, string? gameId, Guid? hostGameId);

    [LoggerMessage(LogLevel.Warning, "Command {CommandType} (key: {IdempotencyKey}) was rejected - duplicate command", SkipEnabledCheck = true)]
    public static partial void LogCommandRejectedAsDuplicate(this ILogger logger, string commandType, Guid idempotencyKey);

    [LoggerMessage(LogLevel.Warning, "Command {CommandType} (key: {IdempotencyKey}) was rejected - validation failed: {ErrorCode}", SkipEnabledCheck = true)]
    public static partial void LogCommandRejectedByValidationFailure(this ILogger logger, string commandType, string idempotencyKey, ErrorCode errorCode);

    [LoggerMessage(LogLevel.Error, "Skid distance is negative: {SkidDistance}", SkipEnabledCheck = true)]
    public static partial void LogSkidDistanceNegative(this ILogger logger, int skidDistance);

    [LoggerMessage(LogLevel.Warning, "Unit not found", SkipEnabledCheck = true)]
    public static partial void LogUnitNotFound(this ILogger logger);

    [LoggerMessage(LogLevel.Error, "Bridge collapse should always result in a fall for unit {UnitId}", SkipEnabledCheck = true)]
    public static partial void LogBridgeCollapseShouldAlwaysResultInFall(this ILogger logger, Guid unitId);

    [LoggerMessage(LogLevel.Information, "Skipping {AttackType} by {AttackerName} with {Limb} against {TargetName}: the attack can no longer be made", SkipEnabledCheck = true)]
    public static partial void LogSkippingOutdatedPhysicalAttack(this ILogger logger, PhysicalAttackType attackType, string attackerName, PartLocation limb, string targetName);

    [LoggerMessage(LogLevel.Information, "Skipping weapon at {Location} attack from {AttackerName} to {TargetName} - attacker has partial cover", SkipEnabledCheck = true)]
    public static partial void LogSkippingWeaponAttackDueToPartialCover(this ILogger logger, PartLocation location, string attackerName, string targetName);

    [LoggerMessage(LogLevel.Warning, "Ignoring {CommandType} command for unit {UnitId} — movement already completed", SkipEnabledCheck = true)]
    public static partial void LogIgnoringCommandForUnitWithCompletedMovement(this ILogger logger, string commandType, Guid unitId);

    [LoggerMessage(LogLevel.Warning, "Rejecting {AttackType} declaration for unit {UnitId}: {ErrorCode}", SkipEnabledCheck = true)]
    public static partial void LogRejectingMovementAttackDeclaration(this ILogger logger, PhysicalAttackType attackType, Guid unitId, ErrorCode? errorCode);

    [LoggerMessage(LogLevel.Warning, "DFA attacker {AttackerName} could not resolve the occupied landing hex {Hex}", SkipEnabledCheck = true)]
    public static partial void LogDfaAttackerCouldNotResolveLandingHex(this ILogger logger, string attackerName, HexCoordinates hex);

    [LoggerMessage(LogLevel.Warning, "Rejecting physical attack declaration for unit {UnitId}: {ErrorCode}", SkipEnabledCheck = true)]
    public static partial void LogRejectingPhysicalAttackDeclaration(this ILogger logger, Guid unitId, ErrorCode errorCode);

    [LoggerMessage(LogLevel.Information, "Skipping {PlayerName} in the physical attack phase: no unit has a physical attack option", SkipEnabledCheck = true)]
    public static partial void LogSkippingPlayerInPhysicalAttackPhase(this ILogger logger, string playerName);

    [LoggerMessage(LogLevel.Warning, "Skipping {AttackType} by {AttackerName}: target {TargetId} is not in the game", SkipEnabledCheck = true)]
    public static partial void LogSkippingPhysicalAttackTargetNotInGame(this ILogger logger, PhysicalAttackType attackType, string attackerName, Guid targetId);

    [LoggerMessage(LogLevel.Information, "JoinGameCommand received: {PlayerName}", SkipEnabledCheck = true)]
    public static partial void LogJoinGameCommandReceived(this ILogger logger, string playerName);

    [LoggerMessage(LogLevel.Information, "UpdatePlayerStatusCommand received: {PlayerId} - {PlayerStatus}", SkipEnabledCheck = true)]
    public static partial void LogUpdatePlayerStatusCommandReceived(this ILogger logger, Guid playerId, PlayerStatus playerStatus);

    [LoggerMessage(LogLevel.Warning, "Rejecting weapon attack declaration for unit {UnitId}: the unit has a pending movement attack", SkipEnabledCheck = true)]
    public static partial void LogRejectingWeaponAttackDeclarationWithPendingMovementAttack(this ILogger logger, Guid unitId);
}
