using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

public interface ICommandTransportAdapter
{
    IReadOnlyList<ITransportPublisher> TransportPublishers { get; }

    /// <summary>
    /// Adds a transport publisher to the adapter
    /// </summary>
    /// <param name="publisher">The publisher to add</param>
    void AddPublisher(ITransportPublisher? publisher);

    /// <summary>
    /// Clears all transport publishers from the adapter and disposes them if they implement IDisposable
    /// </summary>
    void ClearPublishers();

    /// <summary>
    /// Converts an IGameCommand to a TransportMessage and publishes it to all publishers
    /// </summary>
    /// <param name="command">The command to publish</param>
    void PublishCommand(IGameCommand command);

    /// <summary>
    /// Subscribes to transport messages and converts them back to IGameCommand
    /// </summary>
    /// <param name="onCommandReceived">Callback for received commands</param>
    void Initialize(Action<IGameCommand, ITransportPublisher> onCommandReceived);
}