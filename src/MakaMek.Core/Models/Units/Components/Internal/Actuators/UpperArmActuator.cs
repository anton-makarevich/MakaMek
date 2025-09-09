using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class UpperArmActuator : Component
{
    private static readonly int[] UpperArmActuatorSlots = [1];
    public UpperArmActuator() : base("Upper Arm Actuator", UpperArmActuatorSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.UpperArmActuator;
}
