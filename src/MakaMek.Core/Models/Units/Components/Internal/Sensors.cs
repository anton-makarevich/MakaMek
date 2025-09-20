using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal;

public class Sensors : Component
{
    public static readonly InternalDefinition Definition = new(
        "Sensors",
        2, // 2 health points
        MakaMekComponent.Sensors);

    public Sensors(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
