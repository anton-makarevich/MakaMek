using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class HandActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Hand Actuator",
        MakaMekComponent.HandActuator);

    public HandActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
