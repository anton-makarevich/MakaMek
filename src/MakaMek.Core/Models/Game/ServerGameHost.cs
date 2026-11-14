using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Owns the server game of a hosted lobby: creation, background start, command logging, battle
/// map/reset/try-start forwarding and disposal. Extracted from <see cref="GameManager"/> so the
/// manager only orchestrates the lobby lifecycle and never touches the game instance itself.
/// </summary>
internal sealed class ServerGameHost
{
    private readonly ICommandPublisher _commandPublisher;
    private readonly IGameFactory _gameFactory;
    private readonly CommandLoggingSession _logging;
    private ServerGame? _serverGame;

    public ServerGameHost(
        ICommandPublisher commandPublisher,
        IGameFactory gameFactory,
        ILocalizationService localizationService,
        ICommandLoggerFactory commandLoggerFactory)
    {
        _commandPublisher = commandPublisher;
        _gameFactory = gameFactory;
        _logging = new CommandLoggingSession(commandPublisher, commandLoggerFactory, localizationService);
    }

    public Guid? GameId => _serverGame?.Id;

    public bool HasGame => _serverGame != null;

    public bool IsStarted => _serverGame != null && _serverGame.TurnPhase != PhaseNames.Start;

    /// <summary>
    /// Creates the server game, starts its listening loop in the background and attaches the
    /// command logger.
    /// </summary>
    public void Create()
    {
        // Create the game server instance
        _serverGame = _gameFactory.CreateServerGame(_commandPublisher);
        // Start server listening loop in background
        _ = Task.Run(() => _serverGame?.Start());

        _logging.Attach(_serverGame);
    }

    public void SetBattleMap(BattleMap battleMap) => _serverGame?.SetBattleMap(battleMap);

    public void TryStartGame() => _serverGame?.TryStartGame();

    /// <summary>
    /// Disposes the current server game, waiting briefly for disposal to complete, and detaches
    /// the command logger.
    /// </summary>
    public async Task DisposeGameAsync()
    {
        if (_serverGame != null)
        {
            _serverGame.Dispose();

            // Wait a bit for disposal to complete
            await Task.Delay(200);

            _serverGame = null;
        }

        _logging.Detach();
    }

    /// <summary>
    /// Disposes the current server game without waiting and detaches the command logger.
    /// </summary>
    public void DisposeGame()
    {
        _serverGame?.Dispose();
        _serverGame = null;
        _logging.Detach();
    }

    /// <summary>
    /// Disposes the current server game without waiting and without touching the command logger.
    /// </summary>
    public void DisposeGameOnly()
    {
        _serverGame?.Dispose();
        _serverGame = null;
    }

    /// <summary>
    /// Disposes the command logger without unsubscribing its handler.
    /// </summary>
    public void DisposeLogging() => _logging.DisposeLogger();
}
