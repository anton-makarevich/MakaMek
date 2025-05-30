using Sanet.MakaMek.Core.Data.Game.Commands;

namespace Sanet.MakaMek.Core.Services.Transport;

public interface ICommandPublisher
{
    void PublishCommand(IGameCommand command);
    void Subscribe(Action<IGameCommand> onCommandReceived);

    CommandTransportAdapter Adapter { get; }
}