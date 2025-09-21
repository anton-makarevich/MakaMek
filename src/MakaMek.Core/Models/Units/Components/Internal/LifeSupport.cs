using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class LifeSupport(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly InternalDefinition Definition = new(
        "Life Support",
        1, // 1 health point
        MakaMekComponent.LifeSupport,
        2);
}
