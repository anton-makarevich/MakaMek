using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class HandActuator() : Component("Hand Actuator")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.HandActuator;
}
