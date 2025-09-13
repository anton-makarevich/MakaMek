using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class LowerLegActuator() : Component("Lower Leg")
{
    public override MakaMekComponent ComponentType => MakaMekComponent.LowerLegActuator;
    public override bool IsRemovable => false;
}
