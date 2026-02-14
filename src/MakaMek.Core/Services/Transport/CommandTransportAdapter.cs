using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Serialization;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Adapter that bridges between game commands and transport messages
/// </summary>
public partial class CommandTransportAdapter : ICommandTransportAdapter
{
    private readonly List<ITransportPublisher> _transportPublishers = [];
    private Action<IGameCommand, ITransportPublisher>? _onCommandReceived;
    private bool _isInitialized;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        TypeInfoResolver = new RollModifierTypeResolver(),
        WriteIndented = true,
        Converters = {
            new EnumConverter<MakaMekComponent>(),
            new EnumConverter<PartLocation>(),
            new EnumConverter<MovementType>(),
            new EnumConverter<UnitStatus>(),
            new EnumConverter<WeightClass>()
        }
    };
    private readonly Lock _initLock = new();
    private readonly ILogger<CommandTransportAdapter> _logger;

    /// <summary>
    /// Creates a new instance of the CommandTransportAdapter with multiple publishers
    /// </summary>
    /// <param name="transportPublishers">The transport publishers to use</param>
    /// <param name="loggerFactory">Logger factory for logging</param>
    public CommandTransportAdapter(ILoggerFactory loggerFactory, params ITransportPublisher[] transportPublishers)
    {
        _logger = loggerFactory.CreateLogger<CommandTransportAdapter>();
        foreach (var publisher in transportPublishers)
        {
            _transportPublishers.Add(publisher);
        }
    }

    public IReadOnlyList<ITransportPublisher> TransportPublishers => _transportPublishers;

    /// <summary>
    /// Adds a transport publisher to the adapter
    /// </summary>
    /// <param name="publisher">The publisher to add</param>
    public void AddPublisher(ITransportPublisher? publisher)
    {
        if (publisher == null) return;

        // Guard with the same lock to avoid races with Initialize
        lock (_initLock)
        {
            if (_transportPublishers.Contains(publisher)) return;

            _transportPublishers.Add(publisher);

            // Subscribe immediately if a callback is already available (init may be in progress)
            if (_onCommandReceived != null)
            {
                SubscribePublisher(publisher, _onCommandReceived);
            }
        }
    }
    
    /// <summary>
    /// Clears all transport publishers from the adapter and disposes them if they implement IDisposable
    /// </summary>
    public void ClearPublishers()
    {
        // Take a stable snapshot and clear the shared state under lock
        ITransportPublisher[] snapshot;
        lock (_initLock)
        {
            snapshot = _transportPublishers.ToArray();
            _onCommandReceived = null;
            _isInitialized = false;
            _transportPublishers.Clear();
        }

        // Dispose publishers outside the lock
        foreach (var publisher in snapshot)
        {
            if (publisher is not IDisposable disposable) continue;
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing publisher");
            }
        }
    }
    
    /// <summary>
    /// Converts an IGameCommand to a TransportMessage and publishes it to all publishers
    /// </summary>
    /// <param name="command">The command to publish</param>
    public void PublishCommand(IGameCommand command)
    {
        var message = new TransportMessage
        {
            MessageType = command.GetType().Name,
            SourceId = command.GameOriginId,
            Payload = SerializeCommand(command),
            Timestamp = command.Timestamp
        };
        
        // Publish to all transport publishers, isolating per-publisher failures
        ITransportPublisher[] publishersSnapshot;
        lock (_initLock)
        {
            publishersSnapshot = _transportPublishers.ToArray();
        }

        foreach (var publisher in publishersSnapshot)
        {
            try
            {
                publisher.PublishMessage(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing to {PublisherType}", publisher.GetType().Name);
            }
        }
    }
    
    /// <summary>
    /// Subscribes to transport messages and converts them back to IGameCommand
    /// </summary>
    /// <param name="onCommandReceived">Callback for received commands</param>
    public void Initialize(Action<IGameCommand, ITransportPublisher> onCommandReceived)
    {
        ITransportPublisher[] publishersSnapshot;
        lock (_initLock)
        {
            if (_isInitialized)
                return; // Already initialized, do nothing

            _onCommandReceived = onCommandReceived;
            _isInitialized = true; // Close the race window with AddPublisher
            publishersSnapshot = _transportPublishers.ToArray(); // Stable snapshot
        }

        // Subscribe outside the lock to minimize lock hold time
        foreach (var publisher in publishersSnapshot)
        {
            SubscribePublisher(publisher, onCommandReceived);
        }
    }
    
    /// <summary>
    /// Serializes an IGameCommand to a JSON string
    /// </summary>
    /// <param name="command">The command to serialize</param>
    /// <returns>JSON representation of the command</returns>
    private string SerializeCommand(IGameCommand command)
    {
        return JsonSerializer.Serialize(command, command.GetType(), JsonSerializerOptions);
    }
    
    /// <summary>
    /// Deserializes a TransportMessage payload to an IGameCommand
    /// </summary>
    /// <param name="message">The transport message to deserialize</param>
    /// <returns>The deserialized command</returns>
    /// <exception cref="UnknownCommandTypeException">Thrown when the command type is unknown</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the JSON is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when deserialization fails or produces an invalid command</exception>
    internal IGameCommand DeserializeCommand(TransportMessage message)
    {
        var commandType = CommandTypeRegistry.GetCommandType(message.MessageType);
        if (commandType == null)
        {
            // Unknown command type - throw exception
            throw new UnknownCommandTypeException(message.MessageType);
        }
        
        try
        {
            if (JsonSerializer.Deserialize(message.Payload, commandType, JsonSerializerOptions) is not IGameCommand command)
                throw new InvalidOperationException($"Failed to deserialize command of type {message.MessageType}");
            command.GameOriginId = message.SourceId;
            command.Timestamp = message.Timestamp;
            return command;
        }
        catch (JsonException ex)
        {
            // Rethrow JSON deserialization errors
            throw new JsonException($"Error deserializing command of type {message.MessageType}: {ex.Message}", ex);
        }
    }
    
    // Helper method to encapsulate the subscription logic including error handling
    private void SubscribePublisher(ITransportPublisher publisher, Action<IGameCommand, ITransportPublisher> onCommandReceived)
    {
        publisher.Subscribe(message => {
            try
            {
                var command = DeserializeCommand(message);
                onCommandReceived(command, publisher);
            }
            catch (UnknownCommandTypeException uex)
            {
                _logger.LogError(uex, "Unknown command type: {CommandType}", message.MessageType);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "JSON error deserializing command: {CommandType}", message.MessageType);
            }
            catch (Exception ex)
            {
                // Log error but don't crash the transport subscription
                _logger.LogError(ex, "Error processing command: {CommandType}", message.MessageType);
            }
        });
    }
}
