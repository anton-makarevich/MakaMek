using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Sensors(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly InternalDefinition Definition = new(
        "Sensors",
        2, // 2 health points
        MakaMekComponent.Sensors,
        2);

    public static readonly int[] DefaultMountSlots = [1, 4];
}
