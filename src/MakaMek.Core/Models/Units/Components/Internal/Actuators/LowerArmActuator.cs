using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class LowerArmActuator : Component
{
    private static readonly int[] LowerArmSlots = [2];
    public LowerArmActuator() : base("Lower Arm", LowerArmSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.LowerArmActuator;
}
