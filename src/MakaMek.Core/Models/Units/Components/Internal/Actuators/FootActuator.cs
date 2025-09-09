using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class FootActuator : Component
{
    private static readonly int[] FootSlots = [3];
    public FootActuator() : base("Foot Actuator", FootSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.FootActuator;
    public override bool IsRemovable => false;
}
