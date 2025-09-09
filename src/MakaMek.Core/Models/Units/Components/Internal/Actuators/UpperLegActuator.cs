using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class UpperLegActuator : Component
{
    private static readonly int[] UpperLegSlots = [1];
    public UpperLegActuator() : base("Upper Leg", UpperLegSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.UpperLegActuator;
    public override bool IsRemovable => false;
}
