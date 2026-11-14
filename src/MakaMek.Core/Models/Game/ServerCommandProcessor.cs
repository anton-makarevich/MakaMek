using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Validates and dispatches incoming client commands for a server game, including the
/// idempotency check that drops duplicate commands. Extracted from <see cref="ServerGame"/>
/// so the game class only exposes the authoritative state and phase transitions.
/// </summary>
internal sealed class ServerCommandProcessor
{
    private readonly ServerGame _game;
    private readonly ConcurrentDictionary<Guid, byte> _processedCommandKeys = new();

    public ServerCommandProcessor(ServerGame game)
    {
        _game = game;
    }

    public void ResetDuplicateTracking()
    {
        _processedCommandKeys.Clear();
    }

    public void Process(IGameCommand command)
    {
        if (command is not IClientCommand and not RequestGameLobbyStatusCommand) return;
        if (!_game.ShouldHandleCommand(command)) return;

        // Check for duplicate commands using an idempotency key
        if (command is IClientCommand { IdempotencyKey: not null } clientCommand)
        {
            if (!_processedCommandKeys.TryAdd(clientCommand.IdempotencyKey.Value, 0))
            {
                // Duplicate command detected - send error response
                _game.Logger.LogWarning(
                    "Command {CommandType} (key: {IdempotencyKey}) was rejected - duplicate command",
                    command.GetType().Name, clientCommand.IdempotencyKey.Value);
                _game.CommandPublisher.PublishCommand(new ErrorCommand
                {
                    GameOriginId = _game.Id,
                    IdempotencyKey = clientCommand.IdempotencyKey.Value,
                    ErrorCode = ErrorCode.DuplicateCommand,
                    Timestamp = DateTime.UtcNow
                });
                return;
            }
        }

        var validationResult = _game.ValidateCommand(command);
        if (!validationResult.IsValid)
        {
            var failureIdempotencyKey = command is IClientCommand { IdempotencyKey: not null } failedClientCommand
                ? failedClientCommand.IdempotencyKey.Value
                : (Guid?)null;
            _game.Logger.LogWarning(
                "Command {CommandType} (key: {IdempotencyKey}) was rejected - validation failed: {ErrorCode}",
                command.GetType().Name,
                failureIdempotencyKey?.ToString() ?? "none",
                validationResult.ErrorCode ?? ErrorCode.UnknownError);
            _game.CommandPublisher.PublishCommand(new ErrorCommand
            {
                GameOriginId = _game.Id,
                IdempotencyKey = failureIdempotencyKey,
                ErrorCode = validationResult.ErrorCode ?? ErrorCode.UnknownError,
                Timestamp = DateTime.UtcNow
            });
            return;
        }

        // Handle PlayerLeftCommand before delegating to phase
        if (command is PlayerLeftCommand playerLeftCommand)
        {
            _game.HandlePlayerLeft(playerLeftCommand);
            _game.RecordCommand(command);
            return;
        }

        _game.DispatchToPhase(command);
        _game.RecordCommand(command);
    }
}

