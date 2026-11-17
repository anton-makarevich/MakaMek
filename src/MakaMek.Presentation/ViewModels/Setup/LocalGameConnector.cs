using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.Rx;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Subscribes the hosting screen to the command stream and creates the local client game bound to the
/// host's server game, using a local transport publisher when the shared RX transport is available.
/// Kept separate so the view model does not depend on the transport implementation.
/// </summary>
public sealed class LocalGameConnector
{
    private readonly ICommandPublisher _commandPublisher;
    private readonly IGameManager _gameManager;
    private readonly GameSetupSession _session;

    public LocalGameConnector(
        ICommandPublisher commandPublisher,
        IGameManager gameManager,
        GameSetupSession session)
    {
        _commandPublisher = commandPublisher;
        _gameManager = gameManager;
        _session = session;
    }

    /// <summary>
    /// Subscribes (replacing any previous subscription) and reuses an existing local game only when it
    /// is still bound to the current server game.
    /// </summary>
    public void SubscribeAndCreateLocalGame(Action<IGameCommand> handler)
    {
        // Avoid double-subscribing if this flow runs more than once (e.g. when restarting hosting)
        _commandPublisher.Unsubscribe(handler);
        _commandPublisher.Subscribe(handler);

        // Reuse an existing local game only when it is still bound to the current
        // server game. Hosting initialization (local lobby → LAN/online) recreates
        // the server game with a new id; keeping a game bound to the old id would
        // make its ShouldHandleCommand filter drop every command from the real
        // server, silently breaking join acks and SetReady validation.
        if (_session.LocalGame != null && _session.LocalGame.ServerGameId == _gameManager.ServerGameId)
            return;

        var rxPublisher = _commandPublisher.Adapter.TransportPublishers
            .FirstOrDefault(t => t is RxTransportPublisher);
        var localPublisher = rxPublisher != null && _commandPublisher is CommandPublisher shared
            ? new LocalCommandPublisher(shared, rxPublisher)
            : null;

        _session.CreateAndInitializeLocalGame(localPublisher ?? _commandPublisher, _gameManager.ServerGameId);
    }
}
