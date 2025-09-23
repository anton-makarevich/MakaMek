using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public sealed class LowerArmActuator(ComponentData? componentData = null) : Component(Definition, componentData)
{
    public static readonly ActuatorDefinition Definition = new(
        "Lower Arm Actuator",
        MakaMekComponent.LowerArmActuator,
        true);
}
