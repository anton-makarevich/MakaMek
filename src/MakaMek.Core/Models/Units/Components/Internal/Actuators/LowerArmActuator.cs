using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class LowerArmActuator : Component
{
    public static readonly ActuatorDefinition Definition = new(
        "Lower Arm",
        MakaMekComponent.LowerArmActuator);

    public LowerArmActuator(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.LowerArmActuator;
}
