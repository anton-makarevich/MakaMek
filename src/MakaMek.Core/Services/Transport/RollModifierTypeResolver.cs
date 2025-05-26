using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Custom type resolver for RollModifier and its derived types
/// Enables proper serialization/deserialization of abstract RollModifier class
/// </summary>
public class RollModifierTypeResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        var jsonTypeInfo = base.GetTypeInfo(type, options);

        // Configure polymorphic serialization for RollModifier
        if (jsonTypeInfo.Type != typeof(RollModifier)) return jsonTypeInfo;
        jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$type",
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
        };

        // Add all known derived types
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(FallingLevelsModifier), nameof(FallingLevelsModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(DamagedGyroModifier), nameof(DamagedGyroModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(AttackerMovementModifier), nameof(AttackerMovementModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(GunneryRollModifier), nameof(GunneryRollModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(HeatRollModifier), nameof(HeatRollModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(RangeRollModifier), nameof(RangeRollModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(SecondaryTargetModifier), nameof(SecondaryTargetModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(TargetMovementModifier), nameof(TargetMovementModifier)));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(typeof(TerrainRollModifier), nameof(TerrainRollModifier)));

        return jsonTypeInfo;
    }
}
