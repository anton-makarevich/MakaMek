using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Custom type resolver for ComponentSpecificData and its derived types
/// Enables proper serialization/deserialization of ComponentSpecificData polymorphism
/// </summary>
public class ComponentSpecificDataTypeResolver : DefaultJsonTypeInfoResolver
{
    private const string TypeDiscriminatorPropertyName = "$type";

    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        // Configure polymorphic serialization for ComponentSpecificData
        if (jsonTypeInfo.Type == typeof(ComponentSpecificData))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = TypeDiscriminatorPropertyName,
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor
            };

            // Register the known derived types
            jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(EngineStateData), "Engine"));
            jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(AmmoStateData), "Ammo"));
        }

        return jsonTypeInfo;
    }
}
