using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Serialization;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Publishers;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Adapter that bridges between game commands and transport messages
/// </summary>
public partial class CommandTransportAdapter : ICommandTransportAdapter
{
    private readonly List<ITransportPublisher> _transportPublishers = [];
    private Action<IGameCommand, ITransportPublisher>? _onCommandReceived;
    private Action<ITransportPublisher>? _onPublisherDisconnected;
    // Tracks the delegate registered on each publisher's HostDisconnected event so it can be
    // unsubscribed later (Action has no equality semantics beyond delegate reference).
    private readonly Dictionary<ITransportPublisher, Action> _disconnectHandlers = new();
    private bool _isInitialized;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        TypeInfoResolver = new CompositeJsonTypeInfoResolver(
            new RollModifierTypeResolver(),
            new PilotingSkillRollContextTypeResolver(),
            new MovementCostTypeResolver(),
            new DefaultJsonTypeInfoResolver()
        ),
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

            if (_onPublisherDisconnected != null)
            {
                SubscribeDisconnectHandler(publisher, _onPublisherDisconnected);
            }
        }
    }

    /// <summary>
    /// Removes a transport publisher from the adapter without disposing it.
    /// </summary>
    /// <param name="publisher">The publisher to remove</param>
    public void RemovePublisher(ITransportPublisher publisher)
    {
        lock (_initLock)
        {
            _transportPublishers.Remove(publisher);
            UnsubscribeDisconnectHandler(publisher);
        }
    }

    /// <summary>
    /// Clears all transport publishers from the adapter and disposes them if they implement IAsyncDisposable
    /// </summary>
    public async Task ClearPublishers()
    {
        // Take a stable snapshot and clear the shared state under lock
        ITransportPublisher[] snapshot;
        lock (_initLock)
        {
            snapshot = _transportPublishers.ToArray();
            foreach (var publisher in snapshot)
            {
                UnsubscribeDisconnectHandler(publisher);
            }
            _onCommandReceived = null;
            _onPublisherDisconnected = null;
            _isInitialized = false;
            _transportPublishers.Clear();
        }

        // Dispose publishers outside the lock
        foreach (var publisher in snapshot)
        {
            if (publisher is not IAsyncDisposable asyncDisposable) continue;
            try
            {
                await asyncDisposable.DisposeAsync();
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

        _logger.LogDebug(
            "Publishing command {MessageType} (origin {GameOriginId}) to {PublisherCount} publisher(s)",
            message.MessageType,
            message.SourceId,
            publishersSnapshot.Length);

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
    /// Converts an IGameCommand to a TransportMessage and publishes it to a single target publisher.
    /// </summary>
    public void PublishCommand(IGameCommand command, ITransportPublisher targetPublisher)
    {
        var message = new TransportMessage
        {
            MessageType = command.GetType().Name,
            SourceId = command.GameOriginId,
            Payload = SerializeCommand(command),
            Timestamp = command.Timestamp
        };

        _logger.LogDebug(
            "Publishing command {MessageType} (origin {GameOriginId}) to {PublisherType}",
            message.MessageType,
            message.SourceId,
            targetPublisher.GetType().Name);

        try
        {
            targetPublisher.PublishMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing to {PublisherType}", targetPublisher.GetType().Name);
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
    /// Registers a callback invoked when a transport publisher reports that its underlying
    /// connection was lost because the remote host disconnected (e.g. relay host loss).
    /// Only publishers that support disconnect notifications will trigger this callback;
    /// publishers that do not are silently ignored. Only the first registration takes effect,
    /// mirroring the behavior of <see cref="Initialize"/>.
    /// </summary>
    /// <param name="onPublisherDisconnected">Callback invoked with the publisher that lost its connection.</param>
    public void RegisterDisconnectHandler(Action<ITransportPublisher> onPublisherDisconnected)
    {
        ITransportPublisher[] publishersSnapshot;
        lock (_initLock)
        {
            if (_onPublisherDisconnected != null)
                return; // Already registered, do nothing

            _onPublisherDisconnected = onPublisherDisconnected;
            publishersSnapshot = [.. _transportPublishers]; // Stable snapshot
        }

        // Subscribe outside the lock to minimize lock hold time
        foreach (var publisher in publishersSnapshot)
        {
            SubscribeDisconnectHandler(publisher, onPublisherDisconnected);
        }
    }

    /// <summary>
    /// Dispatches a locally-originated command through the same receive path used for
    /// inbound transport messages, so it reaches local subscribers even when no transport
    /// publisher is connected (e.g. after the relay host has disconnected).
    /// </summary>
    /// <param name="command">The command to dispatch to local subscribers.</param>
    /// <param name="sourcePublisher">The publisher the command is attributed to.</param>
    public void DispatchLocalCommand(IGameCommand command, ITransportPublisher sourcePublisher)
    {
        Action<IGameCommand, ITransportPublisher>? onCommandReceived;
        lock (_initLock)
        {
            onCommandReceived = _onCommandReceived;
        }

        if (onCommandReceived == null)
        {
            _logger.LogDebug(
                "No command receive callback registered; local command {MessageType} not dispatched",
                command.GetType().Name);
            return;
        }

        try
        {
            onCommandReceived(command, sourcePublisher);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching local command {MessageType}", command.GetType().Name);
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
            _logger.LogDebug(
                "Received transport message {MessageType} from publisher {PublisherType}",
                message.MessageType,
                publisher.GetType().Name);
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

    // Helper method to encapsulate disconnect-notification subscription logic.
    // Only publishers that expose a HostDisconnected notification (currently RelayClientPublisher)
    // are supported; other publisher types are silently ignored.
    private void SubscribeDisconnectHandler(ITransportPublisher publisher, Action<ITransportPublisher> onPublisherDisconnected)
    {
        if (publisher is not RelayClientPublisher relayPublisher) return;

        lock (_initLock)
        {
            if (_disconnectHandlers.ContainsKey(publisher)) return;

            void Handler() => onPublisherDisconnected(publisher);
            relayPublisher.HostDisconnected += Handler;
            _disconnectHandlers[publisher] = Handler;
        }
    }

    // Helper method to remove a previously registered disconnect-notification subscription.
    private void UnsubscribeDisconnectHandler(ITransportPublisher publisher)
    {
        if (publisher is not RelayClientPublisher relayPublisher) return;

        if (_disconnectHandlers.Remove(publisher, out var handler))
        {
            relayPublisher.HostDisconnected -= handler;
        }
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return new ValueTask(ClearPublishers());
    }
}
