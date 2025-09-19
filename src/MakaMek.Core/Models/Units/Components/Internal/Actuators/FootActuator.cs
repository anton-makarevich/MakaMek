using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class FootActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Foot Actuator",
        MakaMekComponent.FootActuator);

    public FootActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.FootActuator;
}
