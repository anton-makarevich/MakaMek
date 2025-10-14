using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Implementation of ICommandPublisher that uses a CommandTransportAdapter
/// for serialization and transport
/// </summary>
public class CommandPublisher : ICommandPublisher
{
    private readonly List<Action<IGameCommand>> _subscribers = [];
    private readonly Dictionary<Action<IGameCommand>, ITransportPublisher> _subscriberTransports = new();
    
    /// <summary>
    /// Gets the command transport adapter used by this publisher
    /// </summary>
    public CommandTransportAdapter Adapter { get; }

    /// <summary>
    /// Creates a new instance of the CommandPublisher
    /// </summary>
    /// <param name="adapter">The command transport adapter to use</param>
    public CommandPublisher(CommandTransportAdapter adapter)
    {
        Adapter = adapter;
    }
    
    /// <summary>
    /// Publishes a command to all subscribers
    /// </summary>
    /// <param name="command">The command to publish</param>
    public void PublishCommand(IGameCommand command)
    {
        Adapter.PublishCommand(command);
    }

    /// <summary>
    /// Subscribes to receive commands
    /// </summary>
    /// <param name="onCommandReceived">Action to call when a command is received</param>
    /// <param name="transportPublisher"></param>
    public void Subscribe(Action<IGameCommand> onCommandReceived, ITransportPublisher? transportPublisher = null)
    {
        Adapter.Initialize(OnCommandReceived);
        _subscribers.Add(onCommandReceived);
        if (transportPublisher != null)
        {
            _subscriberTransports.Add(onCommandReceived, transportPublisher);
        }
    }

    /// <summary>
    /// Unsubscribes from receiving commands
    /// </summary>
    /// <param name="onCommandReceived">Action to remove from subscribers</param>
    public void Unsubscribe(Action<IGameCommand> onCommandReceived)
    {
        _subscribers.Remove(onCommandReceived);
        _subscriberTransports.Remove(onCommandReceived);
    }

    /// <summary>
    /// Called when a command is received from the transport
    /// </summary>
    /// <param name="command">The received command</param>
    /// <param name="sourcePublisher">A transport publisher to subscribe to, if null subscribe to all</param>
    private void OnCommandReceived(IGameCommand command, ITransportPublisher sourcePublisher)
    {
        foreach (var subscriber in _subscribers)
        {
            try
            {
                _subscriberTransports.TryGetValue(subscriber, out var subTransport); 
                var shouldCall = subTransport == null || subTransport == sourcePublisher;
                if (!shouldCall) continue;
                subscriber(command);
            }
            catch (Exception ex)
            {
                // Log subscriber error
                Console.WriteLine($"Error in command subscriber: {ex.Message}");
            }
        }
    }
}
