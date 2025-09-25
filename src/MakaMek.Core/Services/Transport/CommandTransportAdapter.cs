using System.Text.Json;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Serialization;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Adapter that bridges between game commands and transport messages
/// </summary>
public class CommandTransportAdapter
{
    internal readonly List<ITransportPublisher> TransportPublishers = new();
    private readonly Dictionary<string, Type> _commandTypes;
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
    
    /// <summary>
    /// Creates a new instance of the CommandTransportAdapter with multiple publishers
    /// </summary>
    /// <param name="transportPublishers">The transport publishers to use</param>
    public CommandTransportAdapter(params ITransportPublisher[] transportPublishers)
    {
        foreach (var publisher in transportPublishers)
        {
            TransportPublishers.Add(publisher);
        }
        _commandTypes = InitializeCommandTypeDictionary();
    }
    
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
            if (TransportPublishers.Contains(publisher)) return;

            TransportPublishers.Add(publisher);

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
        // Take a stable snapshot and clear shared state under lock
        ITransportPublisher[] snapshot;
        lock (_initLock)
        {
            snapshot = TransportPublishers.ToArray();
            _onCommandReceived = null;
            _isInitialized = false;
            TransportPublishers.Clear();
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
                Console.WriteLine($"Error disposing publisher: {ex.Message}");
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
            publishersSnapshot = TransportPublishers.ToArray();
        }

        foreach (var publisher in publishersSnapshot)
        {
            try
            {
                publisher.PublishMessage(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing to {publisher.GetType().Name}: {ex.Message}");
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
            _isInitialized = true; // Close race window with AddPublisher
            publishersSnapshot = TransportPublishers.ToArray(); // Stable snapshot
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
        if (!_commandTypes.TryGetValue(message.MessageType, out var commandType))
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
    
    /// <summary>
    /// Initializes a dictionary mapping command type names to their types
    /// This avoids using reflection for type resolution
    /// </summary>
    private Dictionary<string, Type> InitializeCommandTypeDictionary()
    {
        // Explicitly register all command types to avoid reflection
        // This could be auto-generated at build time if needed
        return new Dictionary<string, Type>
        {
            // Client commands
            { nameof(JoinGameCommand), typeof(JoinGameCommand) },
            { nameof(UpdatePlayerStatusCommand), typeof(UpdatePlayerStatusCommand) },
            { nameof(DeployUnitCommand), typeof(DeployUnitCommand) },
            { nameof(MoveUnitCommand), typeof(MoveUnitCommand) },
            { nameof(WeaponConfigurationCommand), typeof(WeaponConfigurationCommand) },
            { nameof(WeaponAttackDeclarationCommand), typeof(WeaponAttackDeclarationCommand) },
            { nameof(PhysicalAttackCommand), typeof(PhysicalAttackCommand) },
            { nameof(TurnEndedCommand), typeof(TurnEndedCommand) },
            { nameof(RollDiceCommand), typeof(RollDiceCommand) },
            { nameof(RequestGameLobbyStatusCommand), typeof(RequestGameLobbyStatusCommand) },
            { nameof(TryStandupCommand), typeof(TryStandupCommand) },
            { nameof(ShutdownUnitCommand), typeof(ShutdownUnitCommand) },
            { nameof(StartupUnitCommand), typeof(StartupUnitCommand) },
            
            // Server commands 
            { nameof(WeaponAttackResolutionCommand), typeof(WeaponAttackResolutionCommand) },
            { nameof(HeatUpdatedCommand), typeof(HeatUpdatedCommand) },
            { nameof(TurnIncrementedCommand), typeof(TurnIncrementedCommand) },
            { nameof(DiceRolledCommand), typeof(DiceRolledCommand) },
            { nameof(ChangePhaseCommand), typeof(ChangePhaseCommand) },
            { nameof(ChangeActivePlayerCommand), typeof(ChangeActivePlayerCommand) },
            { nameof(SetBattleMapCommand), typeof(SetBattleMapCommand) },
            { nameof(MechFallCommand), typeof(MechFallCommand) },
            { nameof(MechStandUpCommand), typeof(MechStandUpCommand) },
            { nameof(PilotConsciousnessRollCommand), typeof(PilotConsciousnessRollCommand)},
            { nameof(UnitShutdownCommand), typeof(UnitShutdownCommand)},
            { nameof(UnitStartupCommand), typeof(UnitStartupCommand) },
            { nameof(AmmoExplosionCommand), typeof(AmmoExplosionCommand) },
            { nameof(CriticalHitsResolutionCommand), typeof(CriticalHitsResolutionCommand) }
        };
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
                Console.WriteLine($"Unknown MessageType '{message.MessageType}' from {message.SourceId}: {uex.Message}");
            }
            catch (JsonException jex)
            {
                Console.WriteLine($"JSON error for '{message.MessageType}' (SourceId={message.SourceId}): {jex.Message}");
            }
            catch (Exception ex)
            {
                // Log error but don't crash the transport subscription
                Console.WriteLine($"Error processing '{message.MessageType}' (SourceId={message.SourceId}): {ex.Message}");
                // Depending on logging strategy, might want a more robust logger here in the future
            }
        });
    }
}
