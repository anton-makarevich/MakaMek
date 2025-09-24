using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Custom type resolver for RollModifier and its derived types
/// Enables proper serialization/deserialization of abstract RollModifier class
/// </summary>
public partial class RollModifierTypeResolver : DefaultJsonTypeInfoResolver
{
    public const string TypeDiscriminatorPropertyName = "$type";
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        // Configure polymorphic serialization for RollModifier
        if (jsonTypeInfo.Type != typeof(RollModifier)) return jsonTypeInfo;
        jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = TypeDiscriminatorPropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
        };

        // Call the generated method to register types found by the source generator
        RegisterGeneratedTypes(jsonTypeInfo);

        return jsonTypeInfo;
    }
    
    /// <summary>
    /// Registers additional RollModifier derived types found by the source generator
    /// This method is implemented by the source generator
    /// </summary>
    static partial void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo);
}
