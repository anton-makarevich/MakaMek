using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class LowerLegActuator() : Component("Lower Leg", LowerLegSlots)
{
    private static readonly int[] LowerLegSlots = [2];

    public override MakaMekComponent ComponentType => MakaMekComponent.LowerLegActuator;
    public override bool IsRemovable => false;
}
