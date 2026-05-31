using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Core.Data.Serialization;

public class MovementCostTypeResolver : PolymorphicTypeResolver<MovementCost>
{
    protected override void RegisterGeneratedTypes(JsonTypeInfo jsonTypeInfo)
    {
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof(ElevationChangeMovementCost), "ElevationChangeMovementCost"));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof(JumpMovementCost), "JumpMovementCost"));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof(RotationMovementCost), "RotationMovementCost"));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof(StandUpAttemptMovementCost), "StandUpAttemptMovementCost"));
        jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(new(typeof(TerrainMovementCost), "TerrainMovementCost"));
    }
}
