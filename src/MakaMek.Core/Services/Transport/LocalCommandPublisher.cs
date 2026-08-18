using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// An <see cref="ICommandPublisher"/> scoped to a single transport publisher (typically Rx loopback).
/// Outbound commands are sent only to the scoped transport; inbound subscriptions receive only
/// commands delivered through the same transport. Used by a co-located <see cref="Game.ClientGame"/>
/// on the host machine to prevent raw client commands from being published to the relay/LAN and
/// duplicated by the server rebroadcast.
/// </summary>
public sealed class LocalCommandPublisher : ICommandPublisher
{
    private readonly CommandPublisher _shared;
    private readonly ITransportPublisher _scopedPublisher;

    public LocalCommandPublisher(CommandPublisher shared, ITransportPublisher scopedPublisher)
    {
        _shared = shared;
        _scopedPublisher = scopedPublisher;
    }

    public ICommandTransportAdapter Adapter => _shared.Adapter;

    public void PublishCommand(IGameCommand command)
    {
        _shared.Adapter.PublishCommand(command, _scopedPublisher);
    }

    public void Subscribe(Action<IGameCommand> onCommandReceived, ITransportPublisher? transportPublisher = null)
    {
        // Always scope inbound to the local transport, ignoring any caller-supplied override.
        _shared.Subscribe(onCommandReceived, _scopedPublisher);
    }

    public void Unsubscribe(Action<IGameCommand> onCommandReceived)
    {
        _shared.Unsubscribe(onCommandReceived);
    }
}
