using System.Text.Json;
using System.Text.Json.Serialization;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace MakaMek.Tools.MtfConverter;

public class ComponentEnumConverter : JsonConverter<MakaMekComponent>
{
    public override MakaMekComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        return Enum.Parse<MakaMekComponent>(stringValue ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, MakaMekComponent value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}