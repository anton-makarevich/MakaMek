using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

namespace Sanet.MakaMek.Core.Data.Serialization;

/// <summary>
/// Custom type resolver for PilotingSkillRollContext and its derived types
/// Enables proper serialization/deserialization of PilotingSkillRollContext records
/// </summary>
public partial class PilotingSkillRollContextTypeResolver : DefaultJsonTypeInfoResolver
{
    public const string TypeDiscriminatorPropertyName = "$type";
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        // Handle PilotingSkillRollContext base type and its derived types
        if (type == typeof(PilotingSkillRollContext) || type.IsSubclassOf(typeof(PilotingSkillRollContext)))
        {
            var jsonTypeInfo = base.GetTypeInfo(type, options);

            // Only configure polymorphism for the base type
            if (type != typeof(PilotingSkillRollContext)) return jsonTypeInfo;
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

        // Handle arrays of PilotingSkillRollContext
        if (type.IsArray && type.GetElementType() == typeof(PilotingSkillRollContext))
        {
            return base.GetTypeInfo(type, options);
        }

        return null;
    }
    
    /// <summary>
    /// Registers additional PilotingSkillRollContext derived types found by the source generator
    /// This method is implemented by the source generator
    /// </summary>
    static partial void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo);
}
