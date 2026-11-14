using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Serialization;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Exceptions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Serializes game commands to transport payloads and back. Owns the JSON options and the
/// polymorphic type resolvers so <see cref="CommandTransportAdapter"/> stays a transport
/// coordinator and does not reference every serialized DTO family.
/// </summary>
internal static class CommandJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
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

    /// <summary>
    /// Serializes a command using its concrete type so record-specific properties are kept.
    /// </summary>
    public static string Serialize(IGameCommand command) =>
        JsonSerializer.Serialize(command, command.GetType(), Options);

    /// <summary>
    /// Deserializes a transport payload to its registered command type.
    /// </summary>
    /// <exception cref="UnknownCommandTypeException">The message type is not a registered command.</exception>
    /// <exception cref="JsonException">The payload is not valid JSON for the command.</exception>
    /// <exception cref="InvalidOperationException">The payload deserialized to something that is not a command.</exception>
    public static IGameCommand Deserialize(TransportMessage message)
    {
        var commandType = CommandTypeRegistry.GetCommandType(message.MessageType);
        if (commandType == null)
        {
            throw new UnknownCommandTypeException(message.MessageType);
        }

        try
        {
            if (JsonSerializer.Deserialize(message.Payload, commandType, Options) is not IGameCommand command)
                throw new InvalidOperationException($"Failed to deserialize command of type {message.MessageType}");
            command.GameOriginId = message.SourceId;
            command.Timestamp = message.Timestamp;
            return command;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Error deserializing command of type {message.MessageType}: {ex.Message}", ex);
        }
    }
}
