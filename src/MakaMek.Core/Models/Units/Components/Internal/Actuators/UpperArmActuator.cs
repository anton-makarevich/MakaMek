using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class UpperArmActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Upper Arm Actuator",
        MakaMekComponent.UpperArmActuator);

    public UpperArmActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
