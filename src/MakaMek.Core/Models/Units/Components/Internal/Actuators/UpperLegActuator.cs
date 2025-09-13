using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class UpperLegActuator() : Component("Upper Leg")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.UpperLegActuator;
    public override bool IsRemovable => false;
}
