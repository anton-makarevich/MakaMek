using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Composite type resolver that chains multiple type resolvers together
/// </summary>
public class CompositeJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    private readonly IJsonTypeInfoResolver[] _resolvers;

    public CompositeJsonTypeInfoResolver(params IJsonTypeInfoResolver[] resolvers)
    {
        _resolvers = resolvers;
    }

    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // Try each resolver in order until one returns a non-null result
        foreach (var resolver in _resolvers)
        {
            var typeInfo = resolver.GetTypeInfo(type, options);
            if (typeInfo != null)
                return typeInfo;
        }

        return null;
    }
}
