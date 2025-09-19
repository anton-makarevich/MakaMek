using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Gyro : Component
{
    public static readonly InternalDefinition Definition = new(
        "Gyro",
        2, // 2 health points
        MakaMekComponent.Gyro,
        4); // 4 slots

    public Gyro(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.Gyro;
}
