using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class LifeSupport : Component
{
    public static readonly InternalDefinition Definition = new(
        "Life Support",
        1, // 1 health point
        MakaMekComponent.LifeSupport);

    public LifeSupport(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.LifeSupport;
}
