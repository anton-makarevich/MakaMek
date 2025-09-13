using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class FootActuator() : Component("Foot Actuator")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.FootActuator;
    public override bool IsRemovable => false;
}
