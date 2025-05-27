using System.Text.Json;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Adapter that bridges between game commands and transport messages
/// </summary>
public class CommandTransportAdapter
{
    internal readonly List<ITransportPublisher> TransportPublishers = new();
    private readonly Dictionary<string, Type> _commandTypes;
    private Action<IGameCommand>? _onCommandReceived;
    private bool _isInitialized;
    
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
        if (publisher == null || TransportPublishers.Contains(publisher)) return;
        TransportPublishers.Add(publisher);
            
        // If Initialize has already been called, subscribe the new publisher immediately
        if (_onCommandReceived != null)
        {
            SubscribePublisher(publisher, _onCommandReceived);
        }
    }
    
    /// <summary>
    /// Clears all transport publishers from the adapter and disposes them if they implement IDisposable
    /// </summary>
    public void ClearPublishers()
    {
        // Properly dispose publishers if they implement IDisposable
        foreach (var publisher in TransportPublishers)
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
        _onCommandReceived = null;
        _isInitialized = false;
        TransportPublishers.Clear();
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
        
        // Publish to all transport publishers
        foreach (var publisher in TransportPublishers)
        {
            publisher.PublishMessage(message);
        }
    }
    
    /// <summary>
    /// Subscribes to transport messages and converts them back to IGameCommand
    /// </summary>
    /// <param name="onCommandReceived">Callback for received commands</param>
    public void Initialize(Action<IGameCommand> onCommandReceived)
    {
        if (_isInitialized)
            return; // Already initialized, do nothing

        _onCommandReceived = onCommandReceived;
        
        // Subscribe to all publishers
        foreach (var publisher in TransportPublishers)
        {
            SubscribePublisher(publisher, onCommandReceived);
        }
        _isInitialized = true;
    }
    
    /// <summary>
    /// Serializes an IGameCommand to a JSON string
    /// </summary>
    /// <param name="command">The command to serialize</param>
    /// <returns>JSON representation of the command</returns>
    private string SerializeCommand(IGameCommand command)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            TypeInfoResolver = new RollModifierTypeResolver()
        };
        return JsonSerializer.Serialize(command, command.GetType(), options);
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
            // Ensure the game origin ID and timestamp are set correctly
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                TypeInfoResolver = new RollModifierTypeResolver()
            };
            
            if (JsonSerializer.Deserialize(message.Payload, commandType, options) is not IGameCommand command)
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
            
            // Server commands 
            { nameof(WeaponAttackResolutionCommand), typeof(WeaponAttackResolutionCommand) },
            { nameof(HeatUpdatedCommand), typeof(HeatUpdatedCommand) },
            { nameof(TurnIncrementedCommand), typeof(TurnIncrementedCommand) },
            { nameof(DiceRolledCommand), typeof(DiceRolledCommand) },
            { nameof(ChangePhaseCommand), typeof(ChangePhaseCommand) },
            { nameof(ChangeActivePlayerCommand), typeof(ChangeActivePlayerCommand) },
            { nameof(SetBattleMapCommand), typeof(SetBattleMapCommand) },
            { nameof(PilotingSkillRollCommand), typeof(PilotingSkillRollCommand) },
            { nameof(MechFallingCommand), typeof(MechFallingCommand) },
        };
    }
    
    // Helper method to encapsulate the subscription logic including error handling
    private void SubscribePublisher(ITransportPublisher publisher, Action<IGameCommand> onCommandReceived)
    {
        publisher.Subscribe(message => {
            try
            {
                var command = DeserializeCommand(message);
                onCommandReceived(command);
            }
            catch (Exception ex)
            {
                // Log error but don't crash the transport subscription
                Console.WriteLine($"Error processing received message: {ex.Message}");
                // Depending on logging strategy, might want a more robust logger here in the future
            }
        });
    }
}
