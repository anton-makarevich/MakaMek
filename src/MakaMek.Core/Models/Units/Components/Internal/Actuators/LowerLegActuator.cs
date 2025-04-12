using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class LowerLegActuator : Component
{
    private static readonly int[] LowerLegSlots = [2];
    public LowerLegActuator() : base("Lower Leg", LowerLegSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.LowerLegActuator;
    public override bool IsRemovable => false;
}
