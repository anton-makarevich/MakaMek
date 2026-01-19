using System.Text.Json;
using System.Text.Json.Serialization;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Core.Data.Serialization.Converters;

/// <summary>
/// JSON converter for IGameCommand that uses CommandTypeRegistry for polymorphic serialization
/// Handles serialization and deserialization of command types with $type property
/// </summary>
public class GameCommandJsonConverter : JsonConverter<IGameCommand>
{
    private const string TypePropertyName = "$type";

    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IGameCommand).IsAssignableFrom(typeToConvert);
    }

    public override IGameCommand? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return null;

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty(TypePropertyName, out var typeProperty))
        {
            throw new JsonException($"Missing '{TypePropertyName}' property for command deserialization");
        }

        var typeName = typeProperty.GetString();
        if (string.IsNullOrEmpty(typeName))
        {
            throw new JsonException($"'{TypePropertyName}' property cannot be null or empty");
        }

        var commandType = CommandTypeRegistry.GetCommandType(typeName);
        if (commandType == null)
        {
            throw new JsonException($"Unknown command type: {typeName}");
        }

        // Deserialize the command using the specific type
        var json = root.GetRawText();
        // this is needed to avoid infinite recursion
        var innerOptions = GetOptionsWithoutThisConverter(options);
        var command = (IGameCommand?)JsonSerializer.Deserialize(json, commandType, innerOptions);

        return command;
    }

    public override void Write(Utf8JsonWriter writer, IGameCommand value, JsonSerializerOptions options)
    {
        var commandType = value.GetType();
        var typeName = CommandTypeRegistry.GetCommandTypeName(commandType);
    
        if (string.IsNullOrEmpty(typeName))
        {
            throw new JsonException($"Cannot find type name for command type {commandType.Name}");
        }

        writer.WriteStartObject();
        writer.WriteString(TypePropertyName, typeName);
    
        // this is needed to avoid infinite recursion
        var innerOptions = GetOptionsWithoutThisConverter(options);
    
        var json = JsonSerializer.Serialize(value, commandType, innerOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
    
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name != TypePropertyName)
            {
                property.WriteTo(writer);
            }
        }
    
        writer.WriteEndObject();
    }
    
    private JsonSerializerOptions GetOptionsWithoutThisConverter(JsonSerializerOptions options)
    {
        // Only create new options if this converter is present
        var serializationOptions = options;
        var converterType = GetType();
        var converterIndex = -1;
    
        for (var i = 0; i < options.Converters.Count; i++)
        {
            if (options.Converters[i].GetType() != converterType) continue;
            converterIndex = i;
            break;
        }

        if (converterIndex < 0) return serializationOptions;
        serializationOptions = new JsonSerializerOptions(options);
        serializationOptions.Converters.RemoveAt(converterIndex);
        return serializationOptions;
    }
}
