using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Core.Data.Serialization.Converters;

/// <summary>
/// Generic JSON converter for enum types that serializes enums as strings
/// Supports both regular property serialization and dictionary key serialization
/// </summary>
/// <typeparam name="T">The enum type to convert</typeparam>
public class EnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        return Enum.Parse<T>(stringValue ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        return Enum.Parse<T>(stringValue ?? string.Empty);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString());
    }
}
