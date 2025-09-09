using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

public class HandActuator : Component
{
    private static readonly int[] HandSlots = [3];
    public HandActuator() : base("Hand Actuator", HandSlots)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.HandActuator;
}
