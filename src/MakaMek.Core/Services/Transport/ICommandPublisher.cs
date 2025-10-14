using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

public interface ICommandPublisher
{
    void PublishCommand(IGameCommand command);
    void Subscribe(Action<IGameCommand> onCommandReceived, ITransportPublisher? transportPublisher = null);
    void Unsubscribe(Action<IGameCommand> onCommandReceived);

    CommandTransportAdapter Adapter { get; }
}