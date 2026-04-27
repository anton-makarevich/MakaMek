using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Custom type resolver for PilotingSkillRollContext and its derived types
/// Enables proper serialization/deserialization of PilotingSkillRollContext records
/// </summary>
public partial class PilotingSkillRollContextTypeResolver : IJsonTypeInfoResolver
{
    private readonly DefaultJsonTypeInfoResolver _default = new();
    
    public const string TypeDiscriminatorPropertyName = "$type";
    
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (type != typeof(PilotingSkillRollContext)
            && !type.IsSubclassOf(typeof(PilotingSkillRollContext))
            && !(type.IsArray && type.GetElementType() == typeof(PilotingSkillRollContext)))
            return null;

        var jsonTypeInfo = _default.GetTypeInfo(type, options);

        if (type == typeof(PilotingSkillRollContext))
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
