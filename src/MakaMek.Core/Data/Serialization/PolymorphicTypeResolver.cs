using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Abstract base class for JSON polymorphic type resolvers.
/// Centralizes common JSON polymorphism configuration logic for derived type resolvers.
/// </summary>
/// <typeparam name="T">The base type for which polymorphic serialization is configured</typeparam>
public abstract class PolymorphicTypeResolver<T> : IJsonTypeInfoResolver
{
    private readonly DefaultJsonTypeInfoResolver _default = new();
    
    public const string TypeDiscriminatorPropertyName = "$type";
    
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (type != typeof(T)
            && !type.IsSubclassOf(typeof(T))
            && !(type.IsArray && type.GetElementType() == typeof(T)))
            return null;

        var jsonTypeInfo = _default.GetTypeInfo(type, options);

        if (type == typeof(T))
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

    /// <summary>
    /// Registers derived types for polymorphic serialization.
    /// Derived classes must implement this to add their specific derived types to the polymorphism options.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type info to configure</param>
    protected abstract void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo);
}
