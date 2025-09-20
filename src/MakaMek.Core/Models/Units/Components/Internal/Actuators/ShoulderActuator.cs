using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class ShoulderActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Shoulder",
        MakaMekComponent.Shoulder);

    public ShoulderActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
