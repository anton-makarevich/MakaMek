using System;
using System.Collections.Generic;
using Sanet.MekForge.Core.Models.Game.Commands;

namespace Sanet.MekForge.Core.Models.Game.Transport;

/// <summary>
/// Implementation of ICommandPublisher that uses a CommandTransportAdapter
/// for serialization and transport
/// </summary>
public class CommandPublisher : ICommandPublisher
{
    private readonly CommandTransportAdapter _adapter;
    private readonly List<Action<IGameCommand>> _subscribers = new();
    
    /// <summary>
    /// Creates a new instance of the CommandPublisher
    /// </summary>
    /// <param name="adapter">The command transport adapter to use</param>
    public CommandPublisher(CommandTransportAdapter adapter)
    {
        _adapter = adapter;
        _adapter.Initialize(OnCommandReceived);
    }
    
    /// <summary>
    /// Publishes a command to all subscribers
    /// </summary>
    /// <param name="command">The command to publish</param>
    public void PublishCommand(IGameCommand command)
    {
        _adapter.PublishCommand(command);
    }
    
    /// <summary>
    /// Subscribes to receive commands
    /// </summary>
    /// <param name="onCommandReceived">Action to call when a command is received</param>
    public void Subscribe(Action<IGameCommand> onCommandReceived)
    {
        _subscribers.Add(onCommandReceived);
    }
    
    /// <summary>
    /// Called when a command is received from the transport
    /// </summary>
    /// <param name="command">The received command</param>
    private void OnCommandReceived(IGameCommand command)
    {
        foreach (var subscriber in _subscribers)
        {
            try
            {
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
