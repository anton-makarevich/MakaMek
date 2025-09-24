using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public sealed class UpperArmActuator(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly ActuatorDefinition Definition = new(
        "Upper Arm Actuator",
        MakaMekComponent.UpperArmActuator,
        true);
}
