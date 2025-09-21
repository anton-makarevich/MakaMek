using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Gyro(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly int[] DefaultMountSlots = [3, 4, 5, 6];
    public static readonly InternalDefinition Definition = new(
        "Gyro",
        2, // 2 health points
        MakaMekComponent.Gyro,
        4); // 4 slots
}
