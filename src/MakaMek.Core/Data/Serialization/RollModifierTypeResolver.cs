using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Custom type resolver for RollModifier and its derived types
/// Enables proper serialization/deserialization of abstract RollModifier class
/// </summary>
public partial class RollModifierTypeResolver : IJsonTypeInfoResolver
{
    private readonly DefaultJsonTypeInfoResolver _default = new();

    private const string TypeDiscriminatorPropertyName = "$type";

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // Only handle RollModifier, its derived types, and arrays of RollModifier
        if (type != typeof(RollModifier)
            && !type.IsSubclassOf(typeof(RollModifier))
            && !(type.IsArray && type.GetElementType() == typeof(RollModifier)))
            return null;

        var jsonTypeInfo = _default.GetTypeInfo(type, options);

        if (type == typeof(RollModifier))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = TypeDiscriminatorPropertyName,
                IgnoreUnrecognizedTypeDiscriminators = false,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
            };

            RegisterGeneratedTypes(jsonTypeInfo);
        }

        return jsonTypeInfo;
    }

    static partial void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo);
}