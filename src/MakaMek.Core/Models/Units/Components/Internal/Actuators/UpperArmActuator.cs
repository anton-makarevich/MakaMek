using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class UpperArmActuator() : Component("Upper Arm Actuator")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.UpperArmActuator;
}
