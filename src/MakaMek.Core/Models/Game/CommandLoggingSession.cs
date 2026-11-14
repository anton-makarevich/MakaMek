using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Services.Logging;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Localization;
using Sanet.Transport.Rx;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Attaches the command logger to the local transport publisher for a server game, and detaches it
/// again on teardown. Extracted from <see cref="GameManager"/> so the manager does not have to know
/// the logging implementation or the transport publisher type it subscribes to.
/// </summary>
internal sealed class CommandLoggingSession(
    ICommandPublisher commandPublisher,
    ICommandLoggerFactory commandLoggerFactory,
    ILocalizationService localizationService)
{
    private ICommandLogger? _commandLogger;
    private Action<IGameCommand>? _logHandler;

    /// <summary>
    /// Creates the logger for the game and subscribes it to the local transport publisher.
    /// </summary>
    public void Attach(ServerGame serverGame)
    {
        var transportAdapter = commandPublisher.Adapter;
        var transportPublisher = transportAdapter.TransportPublishers.FirstOrDefault(ta => ta is RxTransportPublisher);
        _commandLogger = commandLoggerFactory.CreateLogger(localizationService, serverGame);

        _logHandler = SafeLog(_commandLogger);
        commandPublisher.Subscribe(_logHandler, transportPublisher);
    }

    /// <summary>
    /// Unsubscribes the logging handler and disposes the logger.
    /// </summary>
    public void Detach()
    {
        if (_logHandler != null)
        {
            commandPublisher.Unsubscribe(_logHandler);
            _logHandler = null;
        }

        DisposeLogger();
    }

    /// <summary>
    /// Disposes the logger without unsubscribing the handler.
    /// </summary>
    public void DisposeLogger()
    {
        _commandLogger?.Dispose();
        _commandLogger = null;
    }

    private static Action<IGameCommand> SafeLog(ICommandLogger logger) =>
        command =>
        {
            try
            {
                logger.Log(command);
            }
            catch
            {
                // Swallow to avoid impacting a publisher
            }
        };
}
